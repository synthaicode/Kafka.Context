namespace Kafka.Context.Configuration;

public sealed class StreamingOptions
{
    public FlinkStreamingOptions Flink { get; set; } = new();
}

public sealed class FlinkStreamingOptions
{
    /// <summary>
    /// Shortcut for source startup mode (maps to Flink Kafka connector key <c>scan.startup.mode</c>).
    /// If both this property and <see cref="With"/> define the same key, values must match.
    /// </summary>
    public string ScanStartupMode { get; set; } = string.Empty;

    /// <summary>
    /// Shortcut for per-topic source group id (maps to Flink Kafka connector key <c>properties.group.id</c>).
    /// If both this property and <see cref="Sources"/> define the same key, values must match.
    /// </summary>
    public Dictionary<string, string> SourceGroupIdByTopic { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Global Flink Kafka connector WITH(...) properties (applied to both sources and sinks).
    /// </summary>
    public Dictionary<string, string> With { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-topic WITH(...) properties for Flink source tables (CREATE TABLE ... WITH (...)).
    /// Key is Kafka topic name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Sources { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-topic WITH(...) properties for Flink sink tables (CREATE TABLE ... WITH (...)).
    /// Key is Kafka topic name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Sinks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
