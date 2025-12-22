using Kafka.Context.Abstractions;
using Kafka.Context.Application;
using Kafka.Context.Attributes;
using Kafka.Context.Configuration;
using Kafka.Context.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Kafka.Context;

public abstract class KafkaContext : IAsyncDisposable
{
    private readonly Dictionary<Type, string> _topicByEntityType = new();
    private readonly IStreamingQueryRegistry _streamingQueries = new Kafka.Context.Streaming.StreamingQueryRegistry();
    private readonly IStreamingSourceRegistry _streamingSources = new Kafka.Context.Streaming.StreamingSourceRegistry();
    private IReadOnlyList<Type>? _entityTypes;

    protected KafkaContext(KafkaContextOptions options)
        : this(options.Configuration ?? throw new ArgumentNullException(nameof(options.Configuration)), options.LoggerFactory)
    {
    }

    protected KafkaContext(KafkaContextOptions options, ILoggerFactory? loggerFactory)
        : this(options.Configuration ?? throw new ArgumentNullException(nameof(options.Configuration)), loggerFactory ?? options.LoggerFactory)
    {
    }

    protected KafkaContext(IConfiguration configuration)
        : this(configuration, loggerFactory: null)
    {
    }

    protected KafkaContext(IConfiguration configuration, ILoggerFactory? loggerFactory)
        : this(configuration, KsqlDslConfigurationExtensions.DefaultSectionName, loggerFactory)
    {
    }

    protected KafkaContext(IConfiguration configuration, string sectionName)
        : this(configuration, sectionName, loggerFactory: null)
    {
    }

    protected KafkaContext(IConfiguration configuration, string sectionName, ILoggerFactory? loggerFactory)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        SectionName = string.IsNullOrWhiteSpace(sectionName) ? KsqlDslConfigurationExtensions.DefaultSectionName : sectionName;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory?.CreateLogger(GetType());

        Options = configuration.GetKsqlDslOptions(SectionName);

        var modelBuilder = new ModelBuilder(_topicByEntityType, _streamingQueries, _streamingSources);
        OnModelCreating(modelBuilder);
        InitializeEventSets();
        Dlq = new DlqSet(this);
    }

    protected IConfiguration Configuration { get; }
    protected string SectionName { get; }

    public ILogger? Logger { get; }
    public ILoggerFactory? LoggerFactory { get; }
    internal KsqlDslOptions Options { get; }

    public DlqSet Dlq { get; }

    protected virtual void OnModelCreating(IModelBuilder modelBuilder) { }

    public EventSet<T> Set<T>()
    {
        return new EventSet<T>(this);
    }

    public DynamicTopicSet Topic(string topic)
    {
        return new DynamicTopicSet(this, topic);
    }

    public Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        return Kafka.Context.Provisioning.Provisioner.ProvisionAsync(this, cancellationToken);
    }

    public Task ProvisionStreamingAsync(CancellationToken cancellationToken = default)
    {
        var dialectProvider = ResolveStreamingDialectProvider();
        if (dialectProvider is null)
            throw new InvalidOperationException("Streaming dialect provider is not configured. Override ResolveStreamingDialectProvider() or call ProvisionStreamingAsync(IStreamingDialectProvider, ...).");

        return ProvisionStreamingAsync(dialectProvider, cancellationToken);
    }

    public async Task ProvisionStreamingAsync(IStreamingDialectProvider dialectProvider, CancellationToken cancellationToken = default)
    {
        if (dialectProvider is null) throw new ArgumentNullException(nameof(dialectProvider));

        var definitions = _streamingQueries.GetAll();
        if (definitions.Count == 0) return;

        var kindByDerivedType = new Dictionary<Type, StreamingStatementKind>();
        var analyzed = new List<AnalyzedStreamingDefinition>(definitions.Count);

        foreach (var definition in definitions)
        {
            var queryable = definition.QueryFactory(new StreamingQueryBuilder());
            var plan = queryable.Plan;
            var sourceTopics = plan.SourceTypes.Select(GetTopicNameFor).ToArray();
            var hasAgg = plan.HasAggregate || DetectAggregateFunctions(plan);
            plan = plan with { SourceTopics = sourceTopics, HasAggregate = hasAgg, SinkMode = definition.SinkMode };
            var outputTopic = GetTopicNameForStreamingOutput(definition.DerivedType);

            foreach (var sourceType in plan.SourceTypes)
            {
                var inputTopic = GetTopicNameFor(sourceType);
                if (string.Equals(inputTopic, outputTopic, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Streaming loop detected: output topic '{outputTopic}' is also used as an input source (type '{sourceType.FullName}' -> '{inputTopic}').");
                }
            }

            var kind = DetermineStatementKind(plan);
            var suggestedObjectName = outputTopic;
            var objectName = dialectProvider.NormalizeObjectName(suggestedObjectName);

            analyzed.Add(new AnalyzedStreamingDefinition(definition, plan, kind, outputTopic, objectName));
            kindByDerivedType[definition.DerivedType] = kind;
        }

        foreach (var item in analyzed)
        {
            ValidateOutputModeConstraints(item);
            ValidateSinkModeConstraints(item);
            ValidateHavingConstraints(item);
            ValidateWindowConstraints(item);
            ValidateJoinConstraints(item, kindByDerivedType);
        }

        var seenCtas = new Dictionary<string, AnalyzedStreamingDefinition>(StringComparer.Ordinal);
        foreach (var item in analyzed)
        {
            if (item.Kind != StreamingStatementKind.TableCtas) continue;

            if (!seenCtas.TryAdd(item.ObjectName, item))
                throw new InvalidOperationException($"Duplicate CTAS object name detected: '{item.ObjectName}'.");
        }

        if (dialectProvider is IStreamingCatalogDdlProvider catalog)
        {
            var derivedTypes = new HashSet<Type>(definitions.Select(d => d.DerivedType));

            var windowSourceTypes = analyzed
                .Where(a => a.Plan.Window is not null)
                .SelectMany(a => a.Plan.SourceTypes)
                .Where(t => !derivedTypes.Contains(t))
                .Distinct()
                .ToArray();

            foreach (var sourceType in windowSourceTypes)
            {
                if (!_streamingSources.TryGet(sourceType, out var cfg) || cfg.EventTime is null)
                    throw new InvalidOperationException($"Window queries require Flink source event-time configuration for '{sourceType.FullName}'. Call Entity<{sourceType.Name}>().FlinkSource(...).");
            }

            var sourceTypes = analyzed
                .SelectMany(x => x.Plan.SourceTypes)
                .Where(t => !derivedTypes.Contains(t))
                .Distinct()
                .ToArray();

            var sourceDefinitions = new List<StreamingSourceDefinition>(sourceTypes.Length);
            foreach (var sourceType in sourceTypes)
            {
                var topic = GetTopicNameFor(sourceType);
                var obj = dialectProvider.NormalizeObjectName(topic);
                _streamingSources.TryGet(sourceType, out var cfg);
                cfg ??= new StreamingSourceConfig(EventTime: null);

                sourceDefinitions.Add(new StreamingSourceDefinition(sourceType, topic, obj, cfg));
            }

            var sinkDefinitions = new List<StreamingSinkDefinition>(analyzed.Count);
            foreach (var item in analyzed)
            {
                var pk = GetPrimaryKeyColumns(item.Definition.DerivedType);
                if (item.Definition.SinkMode == StreamingSinkMode.Upsert && pk.Count == 0)
                    throw new InvalidOperationException($"SinkMode.Upsert requires at least one [{nameof(Kafka.Context.Attributes.KafkaKeyAttribute)}] on '{item.Definition.DerivedType.FullName}'.");

                sinkDefinitions.Add(new StreamingSinkDefinition(
                    item.Definition.DerivedType,
                    item.OutputTopic,
                    item.ObjectName,
                    item.Definition.SinkMode,
                    pk));
            }

            var sourceDdls = catalog.GenerateSourceDdls(sourceDefinitions);
            foreach (var ddl in sourceDdls)
                await dialectProvider.ExecuteAsync(ddl, cancellationToken).ConfigureAwait(false);

            var sinkDdls = catalog.GenerateSinkDdls(sinkDefinitions);
            foreach (var ddl in sinkDdls)
                await dialectProvider.ExecuteAsync(ddl, cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in analyzed)
        {
            var ddl = dialectProvider.GenerateDdl(item.Plan, item.Kind, item.Definition.OutputMode, item.ObjectName, item.OutputTopic);
            if (string.IsNullOrWhiteSpace(ddl))
                throw new InvalidOperationException($"Dialect provider returned empty DDL for '{item.ObjectName}'.");

            await dialectProvider.ExecuteAsync(ddl, cancellationToken).ConfigureAwait(false);
        }
    }

    public IReadOnlyList<SchemaRegistryAvroPlan> PreviewSchemaRegistryAvro()
    {
        var plans = new List<SchemaRegistryAvroPlan>();

        foreach (var entityType in GetEntityTypes())
        {
            var topicName = GetTopicNameFor(entityType);
            var preview = Kafka.Context.Infrastructure.SchemaRegistry.SchemaRegistryAvroPreview.Build(topicName, entityType);
            plans.Add(SchemaRegistryAvroPlan.From(preview));
        }

        var dlqTopic = GetDlqTopicName();
        var dlqPreview = Kafka.Context.Infrastructure.SchemaRegistry.SchemaRegistryAvroPreview.Build(dlqTopic, typeof(Kafka.Context.Messaging.DlqEnvelope));
        plans.Add(SchemaRegistryAvroPlan.From(dlqPreview));

        return plans;
    }

    internal string GetDlqTopicName()
    {
        return string.IsNullOrWhiteSpace(Options.DlqTopicName) ? "dead_letter_queue" : Options.DlqTopicName;
    }

    internal string GetTopicNameFor(Type entityType)
    {
        if (_topicByEntityType.TryGetValue(entityType, out var topic))
            return topic;

        var kafkaAttr = entityType.GetCustomAttribute<KafkaTopicAttribute>(inherit: true);
        if (kafkaAttr is null)
            throw new InvalidOperationException($"Missing [{nameof(KafkaTopicAttribute)}] on entity type '{entityType.FullName}'.");

        return kafkaAttr.Name;
    }

    internal string GetTopicNameForStreamingOutput(Type derivedType)
    {
        if (_topicByEntityType.TryGetValue(derivedType, out var topic))
            return topic;

        var kafkaAttr = derivedType.GetCustomAttribute<KafkaTopicAttribute>(inherit: true);
        if (kafkaAttr is not null)
            return kafkaAttr.Name;

        return TopicNameConvention.ToKebabCase(derivedType.Name);
    }

    protected virtual IStreamingDialectProvider? ResolveStreamingDialectProvider() => null;

    private static StreamingStatementKind DetermineStatementKind(StreamingQueryPlan plan)
    {
        return plan.HasGroupBy || plan.HasAggregate || plan.HasHaving
            ? StreamingStatementKind.TableCtas
            : StreamingStatementKind.StreamInsert;
    }

    private static void ValidateOutputModeConstraints(AnalyzedStreamingDefinition item)
    {
        if (item.Definition.OutputMode == StreamingOutputMode.Changelog)
            return;

        if (item.Definition.OutputMode == StreamingOutputMode.Final)
        {
            if (item.Plan.Window is null)
                throw new InvalidOperationException("OutputMode.Final requires a window query (TUMBLE/HOP).");
            return;
        }

        throw new InvalidOperationException($"Unknown output mode: {item.Definition.OutputMode}.");
    }

    private static void ValidateSinkModeConstraints(AnalyzedStreamingDefinition item)
    {
        if (item.Plan.SinkMode == StreamingSinkMode.AppendOnly)
            return;

        if (item.Plan.SinkMode == StreamingSinkMode.Upsert)
        {
            if (item.Plan.Window is null)
                throw new InvalidOperationException("SinkMode.Upsert requires a window query (TUMBLE/HOP).");

            if (item.Kind != StreamingStatementKind.TableCtas)
                throw new InvalidOperationException("SinkMode.Upsert is only supported for CTAS (TABLE) queries.");

            return;
        }

        throw new InvalidOperationException($"Unknown sink mode: {item.Plan.SinkMode}.");
    }

    private static void ValidateHavingConstraints(AnalyzedStreamingDefinition item)
    {
        if (!item.Plan.HasHaving)
            return;

        if (item.Plan.HavingPredicate is null)
            throw new InvalidOperationException("Having is marked but no predicate was provided.");

        if (!item.Plan.HasGroupBy && !item.Plan.HasAggregate)
            throw new InvalidOperationException("HAVING requires GROUP BY/aggregate. Use WHERE for row-level filtering.");

        if (item.Plan.SelectSelector is null)
            throw new InvalidOperationException("HAVING requires an explicit Select(...) projection in this version.");
    }

    private static bool DetectAggregateFunctions(StreamingQueryPlan plan)
    {
        var visitor = new AggregateFunctionVisitor();

        if (plan.SelectSelector is not null) visitor.Visit(plan.SelectSelector.Body);
        if (plan.GroupByKeySelector is not null) visitor.Visit(plan.GroupByKeySelector.Body);
        if (plan.HavingPredicate is not null) visitor.Visit(plan.HavingPredicate.Body);

        foreach (var w in plan.WherePredicates)
            visitor.Visit(w.Body);

        foreach (var j in plan.JoinPredicates)
            visitor.Visit(j.Body);

        return visitor.HasAggregate;
    }

    private sealed class AggregateFunctionVisitor : System.Linq.Expressions.ExpressionVisitor
    {
        public bool HasAggregate { get; private set; }

        protected override System.Linq.Expressions.Expression VisitMethodCall(System.Linq.Expressions.MethodCallExpression node)
        {
            if (node.Method.DeclaringType?.FullName == "Kafka.Context.Streaming.Flink.FlinkAgg")
                HasAggregate = true;

            return base.VisitMethodCall(node);
        }
    }

    private static void ValidateJoinConstraints(
        AnalyzedStreamingDefinition item,
        IReadOnlyDictionary<Type, StreamingStatementKind> kindByDerivedType)
    {
        if (item.Plan.JoinPredicates.Count == 0) return;

        // "JOIN -> Window" is forbidden: if the current query is CTAS, it implies aggregation after join.
        if (item.Kind == StreamingStatementKind.TableCtas)
            throw new InvalidOperationException("JOIN with CTAS (JOIN -> aggregation/window) is not supported. Split into (CTAS) then (JOIN).");

        foreach (var join in item.Plan.JoinPredicates)
        {
            if (!IsKeyEqualityOnly(join))
                throw new InvalidOperationException("JOIN ON must be key-equality only (and/or conjunction of equalities).");
        }

        var ctasDerivedSources = item.Plan.SourceTypes
            .Where(t => kindByDerivedType.TryGetValue(t, out var kind) && kind == StreamingStatementKind.TableCtas)
            .ToArray();

        if (ctasDerivedSources.Length != 1)
            throw new InvalidOperationException("Window/CTAS + JOIN constraint: exactly one JOIN side must be a CTAS-derived source; stream-stream or CTAS×CTAS joins are not supported.");

        var otherDerivedSources = item.Plan.SourceTypes
            .Where(t => kindByDerivedType.ContainsKey(t) && !ctasDerivedSources.Contains(t))
            .ToArray();

        if (otherDerivedSources.Length != 0)
            throw new InvalidOperationException("Window/CTAS + JOIN constraint: derived sources other than the CTAS side are not allowed.");
    }

    private static void ValidateWindowConstraints(AnalyzedStreamingDefinition item)
    {
        if (item.Plan.Window is null) return;

        if (item.Plan.SourceTypes.Count != 1)
            throw new InvalidOperationException("Window TVF requires a single input source (no join).");

        if (item.Kind != StreamingStatementKind.TableCtas)
            throw new InvalidOperationException("Window TVF requires CTAS (aggregation). Add GroupBy/Aggregate/Having.");

        if (item.Plan.GroupByKeySelector is null)
            throw new InvalidOperationException("Window TVF requires GroupBy that includes window_start/window_end.");

        var (hasStart, hasEnd) = FindFlinkWindowBoundaries(item.Plan.GroupByKeySelector);
        if (!hasStart || !hasEnd)
            throw new InvalidOperationException("Window GroupBy must include both FlinkWindow.Start() and FlinkWindow.End().");

        if (string.IsNullOrWhiteSpace(item.Plan.Window.TimeColumnName))
            throw new InvalidOperationException("Window TVF requires a time column name.");
    }

    private static (bool HasStart, bool HasEnd) FindFlinkWindowBoundaries(System.Linq.Expressions.LambdaExpression selector)
    {
        var visitor = new FlinkWindowBoundaryVisitor();
        visitor.Visit(selector.Body);
        return (visitor.HasStart, visitor.HasEnd);
    }

    private sealed class FlinkWindowBoundaryVisitor : System.Linq.Expressions.ExpressionVisitor
    {
        public bool HasStart { get; private set; }
        public bool HasEnd { get; private set; }

        protected override System.Linq.Expressions.Expression VisitMethodCall(System.Linq.Expressions.MethodCallExpression node)
        {
            if (node.Method.DeclaringType?.FullName == "Kafka.Context.Streaming.Flink.FlinkWindow")
            {
                if (string.Equals(node.Method.Name, "Start", StringComparison.Ordinal))
                    HasStart = true;
                else if (string.Equals(node.Method.Name, "End", StringComparison.Ordinal))
                    HasEnd = true;
            }

            return base.VisitMethodCall(node);
        }
    }

    private static bool IsKeyEqualityOnly(System.Linq.Expressions.LambdaExpression predicate)
    {
        var body = predicate.Body;
        return IsConjunctionOfEqualities(body, predicate.Parameters);
    }

    private static bool IsConjunctionOfEqualities(
        System.Linq.Expressions.Expression expr,
        IReadOnlyList<System.Linq.Expressions.ParameterExpression> parameters)
    {
        expr = StripConvert(expr);

        if (expr is System.Linq.Expressions.BinaryExpression b && b.NodeType == System.Linq.Expressions.ExpressionType.AndAlso)
            return IsConjunctionOfEqualities(b.Left, parameters) && IsConjunctionOfEqualities(b.Right, parameters);

        if (expr is System.Linq.Expressions.BinaryExpression eq && eq.NodeType == System.Linq.Expressions.ExpressionType.Equal)
            return IsJoinKeyMember(eq.Left, parameters) && IsJoinKeyMember(eq.Right, parameters);

        return false;
    }

    private static bool IsJoinKeyMember(
        System.Linq.Expressions.Expression expr,
        IReadOnlyList<System.Linq.Expressions.ParameterExpression> parameters)
    {
        expr = StripConvert(expr);

        if (expr is not System.Linq.Expressions.MemberExpression m)
            return false;

        var root = StripConvert(m.Expression!);
        return root is System.Linq.Expressions.ParameterExpression p && parameters.Contains(p);
    }

    private static System.Linq.Expressions.Expression StripConvert(System.Linq.Expressions.Expression expr)
    {
        while (expr is System.Linq.Expressions.UnaryExpression u &&
               (u.NodeType == System.Linq.Expressions.ExpressionType.Convert || u.NodeType == System.Linq.Expressions.ExpressionType.ConvertChecked))
        {
            expr = u.Operand;
        }

        return expr;
    }

    private void InitializeEventSets()
    {
        var entityTypes = new List<Type>();
        foreach (var property in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!property.CanWrite) continue;
            if (!property.PropertyType.IsGenericType) continue;
            if (property.PropertyType.GetGenericTypeDefinition() != typeof(EventSet<>)) continue;

            var entityType = property.PropertyType.GetGenericArguments()[0];
            entityTypes.Add(entityType);
            var eventSetType = typeof(EventSet<>).MakeGenericType(entityType);
            var instance = Activator.CreateInstance(eventSetType, this);
            property.SetValue(this, instance);
        }

        _entityTypes = entityTypes.Distinct().ToList();
    }

    internal IReadOnlyList<Type> GetEntityTypes()
    {
        return _entityTypes ?? Array.Empty<Type>();
    }

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private sealed class ModelBuilder : IModelBuilder, IStreamingQueryRegistryProvider, IStreamingSourceRegistryProvider
    {
        private readonly Dictionary<Type, string> _topicByEntityType;
        private readonly IStreamingQueryRegistry _streamingQueries;
        private readonly IStreamingSourceRegistry _streamingSources;

        public ModelBuilder(Dictionary<Type, string> topicByEntityType, IStreamingQueryRegistry streamingQueries, IStreamingSourceRegistry streamingSources)
        {
            _topicByEntityType = topicByEntityType;
            _streamingQueries = streamingQueries;
            _streamingSources = streamingSources;
        }

        public EntityModelBuilder<T> Entity<T>()
        {
            var entityType = typeof(T);
            var kafkaAttr = entityType.GetCustomAttribute<KafkaTopicAttribute>(inherit: true);
            if (kafkaAttr is not null)
                _topicByEntityType[entityType] = kafkaAttr.Name;

            return new EntityModelBuilder<T>(this);
        }

        public IStreamingQueryRegistry StreamingQueries => _streamingQueries;
        public IStreamingSourceRegistry StreamingSources => _streamingSources;
    }

    private sealed record AnalyzedStreamingDefinition(
        StreamingQueryDefinition Definition,
        StreamingQueryPlan Plan,
        StreamingStatementKind Kind,
        string OutputTopic,
        string ObjectName);

    private static IReadOnlyList<string> GetPrimaryKeyColumns(Type entityType)
    {
        var keys = entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<Kafka.Context.Attributes.KafkaKeyAttribute>(inherit: true) is not null)
            .Select(p => p.Name)
            .ToArray();

        return keys;
    }

    private static class TopicNameConvention
    {
        public static string ToKebabCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be null or empty.", nameof(value));

            var sb = new System.Text.StringBuilder(value.Length + 8);

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == '_')
                {
                    sb.Append('-');
                    continue;
                }

                if (!char.IsUpper(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    continue;
                }

                var isWordBoundary =
                    i > 0 &&
                    (char.IsLower(value[i - 1]) ||
                     (i + 1 < value.Length && char.IsLower(value[i + 1])));

                if (isWordBoundary)
                    sb.Append('-');

                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }
    }
}
