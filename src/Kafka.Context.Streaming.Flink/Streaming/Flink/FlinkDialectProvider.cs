using Kafka.Context.Streaming;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Context.Streaming.Flink;

public sealed class FlinkDialectProvider : IStreamingDialectProvider, IStreamingCatalogDdlProvider
{
    private readonly Func<string, CancellationToken, Task> _executeAsync;
    private readonly FlinkKafkaConnectorOptions _kafka;

    public FlinkDialectProvider(Func<string, CancellationToken, Task> executeAsync)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _kafka = new FlinkKafkaConnectorOptions();
    }

    public FlinkDialectProvider(FlinkKafkaConnectorOptions kafka, Func<string, CancellationToken, Task> executeAsync)
    {
        _kafka = kafka ?? throw new ArgumentNullException(nameof(kafka));
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
    }

    public string NormalizeObjectName(string suggestedObjectName)
    {
        if (string.IsNullOrWhiteSpace(suggestedObjectName))
            throw new ArgumentException("Object name cannot be null or empty.", nameof(suggestedObjectName));

        var normalized = suggestedObjectName
            .Trim()
            .Replace('-', '_')
            .ToLowerInvariant();

        normalized = new string(normalized.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        while (normalized.Contains("__", StringComparison.Ordinal))
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);

        normalized = normalized.Trim('_');

        if (normalized.Length == 0)
            normalized = "t";

        if (char.IsDigit(normalized[0]))
            normalized = "t_" + normalized;

        return normalized;
    }

    public string GenerateDdl(StreamingQueryPlan plan, StreamingStatementKind kind, StreamingOutputMode outputMode, string objectName, string outputTopic)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (string.IsNullOrWhiteSpace(objectName)) throw new ArgumentException("Object name cannot be null or empty.", nameof(objectName));
        if (string.IsNullOrWhiteSpace(outputTopic)) throw new ArgumentException("Output topic cannot be null or empty.", nameof(outputTopic));

        if (outputMode == StreamingOutputMode.Final && plan.Window is null)
            throw new InvalidOperationException("OutputMode.Final is only supported for window queries (TUMBLE/HOP) in Flink dialect.");

        var renderer = new FlinkSqlRenderer();
        var query = renderer.RenderSelect(plan);

        var target = FlinkSqlIdentifiers.QuoteIdentifier(NormalizeObjectName(objectName));

        var ddlBody = kind switch
        {
            StreamingStatementKind.TableCtas => $"INSERT INTO {target} {query};",
            StreamingStatementKind.StreamInsert => $"INSERT INTO {target} {query};",
            _ => throw new InvalidOperationException($"Unknown statement kind: {kind}.")
        };

        if (plan.SinkMode == StreamingSinkMode.Upsert)
            return $"-- sink: upsert (infra-dependent)\n{ddlBody}";

        return ddlBody;
    }

    public IReadOnlyList<string> GenerateSourceDdls(IReadOnlyList<StreamingSourceDefinition> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));

        var renderer = new FlinkSqlRenderer();
        var ddls = new List<string>(sources.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            if (!seen.Add(source.ObjectName))
                continue;

            var table = FlinkSqlIdentifiers.QuoteIdentifier(NormalizeObjectName(source.ObjectName));
            var columns = RenderColumns(source.EntityType, source.Config, isSink: false);
            var with = RenderKafkaWithClause(source.TopicName, isSink: false);

            var warning = string.IsNullOrWhiteSpace(_kafka.SchemaRegistryUrl)
                ? $"-- WARNING: '{FlinkKafkaConnectorOptions.SupportedFormat}' typically requires 'schema.registry.url'\n"
                : "";

            ddls.Add($"{warning}CREATE TABLE IF NOT EXISTS {table} (\n{columns}\n) WITH (\n{with}\n);");
        }

        return ddls;
    }

    public IReadOnlyList<string> GenerateSinkDdls(IReadOnlyList<StreamingSinkDefinition> sinks)
    {
        if (sinks is null) throw new ArgumentNullException(nameof(sinks));

        var renderer = new FlinkSqlRenderer();
        var ddls = new List<string>(sinks.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sink in sinks)
        {
            if (!seen.Add(sink.ObjectName))
                continue;

            var table = FlinkSqlIdentifiers.QuoteIdentifier(NormalizeObjectName(sink.ObjectName));
            var columns = RenderColumns(sink.EntityType, config: null, isSink: true, primaryKeys: sink.PrimaryKeyColumns);
            var with = RenderKafkaWithClause(sink.TopicName, isSink: true);

            var warning = string.IsNullOrWhiteSpace(_kafka.SchemaRegistryUrl)
                ? $"-- WARNING: '{FlinkKafkaConnectorOptions.SupportedFormat}' typically requires 'schema.registry.url'\n"
                : "";

            ddls.Add($"{warning}CREATE TABLE IF NOT EXISTS {table} (\n{columns}\n) WITH (\n{with}\n);");
        }

        return ddls;
    }

    public Task ExecuteAsync(string ddl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ddl))
            throw new ArgumentException("DDL cannot be null or empty.", nameof(ddl));

        return _executeAsync(ddl, cancellationToken);
    }

    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "values",
        "coalesce",
    };

    private string RenderKafkaWithClause(string topicName, bool isSink)
    {
        if (!string.Equals(_kafka.Format, FlinkKafkaConnectorOptions.SupportedFormat, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported Flink Kafka format '{_kafka.Format}'. Only '{FlinkKafkaConnectorOptions.SupportedFormat}' is supported.");

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["connector"] = "kafka",
            ["topic"] = topicName,
        };

        if (!string.IsNullOrWhiteSpace(_kafka.BootstrapServers))
            props["properties.bootstrap.servers"] = _kafka.BootstrapServers;

        props["format"] = _kafka.Format;

        if (!string.IsNullOrWhiteSpace(_kafka.SchemaRegistryUrl))
            props["schema.registry.url"] = _kafka.SchemaRegistryUrl;

        MergeWith(props, _kafka.WithProperties, allowOverrideExisting: false);

        if (!isSink && _kafka.SourceWithByTopic.TryGetValue(topicName, out var sourceProps))
            MergeWith(props, sourceProps, allowOverrideExisting: true);

        if (isSink && _kafka.SinkWithByTopic.TryGetValue(topicName, out var sinkProps))
            MergeWith(props, sinkProps, allowOverrideExisting: true);

        // Common.AdditionalProperties is treated as "extra" (not an override mechanism).
        MergeWith(props, _kafka.AdditionalProperties, allowOverrideExisting: false);

        ValidateFlinkKafkaWith(props, isSink);

        return string.Join(",\n", props.Select(p => $"  '{p.Key}' = '{EscapeSingleQuotes(p.Value)}'"));
    }

    private static void MergeWith(Dictionary<string, string> target, IEnumerable<KeyValuePair<string, string>> additions, bool allowOverrideExisting)
    {
        foreach (var (k, v) in additions)
        {
            if (string.IsNullOrWhiteSpace(k))
                continue;

            if (IsProtectedWithKey(k))
                throw new InvalidOperationException($"WITH property '{k}' is reserved and cannot be overridden.");

            if (target.TryAdd(k, v))
                continue;

            if (string.Equals(target[k], v, StringComparison.Ordinal))
                continue;

            if (!allowOverrideExisting)
                throw new InvalidOperationException($"Duplicate WITH property key detected: '{k}'.");

            target[k] = v;
        }
    }

    private static bool IsProtectedWithKey(string key)
    {
        return string.Equals(key, "connector", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "topic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "format", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "properties.bootstrap.servers", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "schema.registry.url", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateFlinkKafkaWith(IReadOnlyDictionary<string, string> props, bool isSink)
    {
        // scan.startup.mode is a source-only setting; ignore it for sinks.
        if (isSink)
            return;

        if (!props.TryGetValue("scan.startup.mode", out var mode) || string.IsNullOrWhiteSpace(mode))
            return;

        mode = mode.Trim();

        // Commonly supported Flink Kafka connector modes.
        // We intentionally keep this list small and fail-fast on unknown values.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "earliest-offset",
            "latest-offset",
            "group-offsets",
            "timestamp",
            "specific-offsets",
        };

        if (!allowed.Contains(mode))
            throw new InvalidOperationException($"Unsupported scan.startup.mode '{mode}'. Allowed: earliest-offset, latest-offset, group-offsets, timestamp, specific-offsets.");

        if (string.Equals(mode, "timestamp", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue("scan.startup.timestamp-millis", out var millis) || string.IsNullOrWhiteSpace(millis))
                throw new InvalidOperationException("scan.startup.mode=timestamp requires 'scan.startup.timestamp-millis'.");

            if (!long.TryParse(millis, out var _))
                throw new InvalidOperationException("scan.startup.timestamp-millis must be an integer (milliseconds since epoch).");
        }

        if (string.Equals(mode, "specific-offsets", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue("scan.startup.specific-offsets", out var offsets) || string.IsNullOrWhiteSpace(offsets))
                throw new InvalidOperationException("scan.startup.mode=specific-offsets requires 'scan.startup.specific-offsets'.");
        }
    }

    private static string EscapeSingleQuotes(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private string RenderColumns(Type entityType, StreamingSourceConfig? config, bool isSink, IReadOnlyList<string>? primaryKeys = null)
    {
        var cols = new List<string>();
        var props = entityType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        StreamingEventTimeConfig? eventTime = config?.EventTime;

        foreach (var p in props)
        {
            if (!p.CanRead) continue;

            var colName = NormalizeColumnName(p.Name);
            var flinkType = MapClrToFlinkType(p.PropertyType, p, isSink, eventTime?.ColumnName == p.Name ? eventTime : null);

            if (eventTime is not null && eventTime.Source == StreamingTimestampSource.KafkaRecordTimestamp && eventTime.ColumnName == p.Name)
            {
                cols.Add($"  `{colName}` {flinkType} METADATA FROM 'timestamp' VIRTUAL");
                continue;
            }

            cols.Add($"  `{colName}` {flinkType}");
        }

        if (eventTime is not null)
        {
            var col = NormalizeColumnName(eventTime.ColumnName);
            cols.Add($"  WATERMARK FOR `{col}` AS `{col}` - {RenderInterval(eventTime.WatermarkDelay)}");
        }

        if (isSink && primaryKeys is { Count: > 0 })
        {
            var pkCols = string.Join(", ", primaryKeys.Select(k => $"`{NormalizeColumnName(k)}`"));
            cols.Add($"  PRIMARY KEY ({pkCols}) NOT ENFORCED");
        }

        return string.Join(",\n", cols);
    }

    private static string NormalizeColumnName(string suggested)
    {
        if (string.IsNullOrWhiteSpace(suggested))
            return "t";

        var normalized = suggested.Trim().Replace('-', '_');
        if (ReservedWords.Contains(normalized))
            normalized = "t_" + normalized;

        return normalized;
    }

    private static string MapClrToFlinkType(Type clrType, System.Reflection.PropertyInfo prop, bool isSink, StreamingEventTimeConfig? eventTime)
    {
        var t = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (t == typeof(string)) return "STRING";
        if (t == typeof(bool)) return "BOOLEAN";
        if (t == typeof(byte)) return "TINYINT";
        if (t == typeof(short)) return "SMALLINT";
        if (t == typeof(int)) return "INT";
        if (t == typeof(long)) return "BIGINT";
        if (t == typeof(float)) return "FLOAT";
        if (t == typeof(double)) return "DOUBLE";
        if (t == typeof(Guid)) return "STRING";

        if (t == typeof(DateTime))
            return eventTime?.Source == StreamingTimestampSource.KafkaRecordTimestamp ? "TIMESTAMP_LTZ(3)" : "TIMESTAMP(3)";

        if (t == typeof(DateTimeOffset))
            return "TIMESTAMP_LTZ(3)";

        if (t == typeof(decimal))
        {
            var attr = prop.GetCustomAttributes(inherit: true).FirstOrDefault(a => a.GetType().FullName == "Kafka.Context.Attributes.KafkaDecimalAttribute");
            if (attr is not null)
            {
                var precisionProp = attr.GetType().GetProperty("Precision");
                var scaleProp = attr.GetType().GetProperty("Scale");
                var precision = precisionProp is null ? 38 : (int)precisionProp.GetValue(attr)!;
                var scale = scaleProp is null ? 18 : (int)scaleProp.GetValue(attr)!;
                return $"DECIMAL({precision}, {scale})";
            }

            return "DECIMAL(38, 18)";
        }

        if (t == typeof(object))
        {
            // Used by FlinkWindow.Start/End() projections in examples.
            return "TIMESTAMP(3)";
        }

        throw new NotSupportedException($"Unsupported CLR type for Flink table column: {clrType.FullName} (property '{prop.DeclaringType?.FullName}.{prop.Name}').");
    }

    private static string RenderInterval(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            throw new InvalidOperationException("Watermark delay cannot be negative.");

        if (span == TimeSpan.Zero)
            return "INTERVAL '0' SECOND";

        if (span.TotalDays >= 1 && span.TotalDays % 1 == 0)
            return $"INTERVAL '{(int)span.TotalDays}' DAY";

        if (span.TotalHours >= 1 && span.TotalHours % 1 == 0)
            return $"INTERVAL '{(int)span.TotalHours}' HOUR";

        if (span.TotalMinutes >= 1 && span.TotalMinutes % 1 == 0)
            return $"INTERVAL '{(int)span.TotalMinutes}' MINUTE";

        if (span.TotalSeconds >= 1 && span.TotalSeconds % 1 == 0)
            return $"INTERVAL '{(int)span.TotalSeconds}' SECOND";

        if (span.TotalMilliseconds >= 1 && span.TotalMilliseconds % 1 == 0)
            return $"INTERVAL '{(int)span.TotalMilliseconds}' MILLISECOND";

        throw new InvalidOperationException($"Unsupported watermark delay: {span}.");
    }
}
