using Kafka.Context.Abstractions;
using Kafka.Context.Application;
using Kafka.Context.Attributes;
using Kafka.Context.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Kafka.Context;

public abstract class KafkaContext : IAsyncDisposable
{
    private readonly Dictionary<Type, string> _topicByEntityType = new();
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

        var modelBuilder = new ModelBuilder(_topicByEntityType);
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

    public Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        return Kafka.Context.Provisioning.Provisioner.ProvisionAsync(this, cancellationToken);
    }

    internal string GetDlqTopicName()
    {
        return string.IsNullOrWhiteSpace(Options.DlqTopicName) ? "dead_letter_queue" : Options.DlqTopicName;
    }

    internal string GetTopicNameFor(Type entityType)
    {
        if (_topicByEntityType.TryGetValue(entityType, out var topic))
            return topic;

        var attr = entityType.GetCustomAttribute<KsqlTopicAttribute>(inherit: true);
        if (attr is null)
            throw new InvalidOperationException($"Missing [{nameof(KsqlTopicAttribute)}] on entity type '{entityType.FullName}'.");

        return attr.Name;
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

    private sealed class ModelBuilder : IModelBuilder
    {
        private readonly Dictionary<Type, string> _topicByEntityType;

        public ModelBuilder(Dictionary<Type, string> topicByEntityType)
        {
            _topicByEntityType = topicByEntityType;
        }

        public void Entity<T>()
        {
            var entityType = typeof(T);
            var attr = entityType.GetCustomAttribute<KsqlTopicAttribute>(inherit: true);
            if (attr is null)
                throw new InvalidOperationException($"Missing [{nameof(KsqlTopicAttribute)}] on entity type '{entityType.FullName}'.");

            _topicByEntityType[entityType] = attr.Name;
        }
    }
}
