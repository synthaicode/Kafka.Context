using System.Collections.Generic;

namespace Kafka.Context.Streaming.Flink;

public sealed class FlinkKafkaConnectorOptions
{
    public const string SupportedFormat = "avro-confluent";

    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Flink format name. Currently only <see cref="SupportedFormat"/> is supported.
    /// </summary>
    public string Format { get; set; } = SupportedFormat;

    /// <summary>
    /// Optional Schema Registry URL (format-dependent).
    /// </summary>
    public string SchemaRegistryUrl { get; set; } = string.Empty;

    public Dictionary<string, string> AdditionalProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Global Flink Kafka connector WITH(...) properties (applied to both sources and sinks).
    /// </summary>
    public Dictionary<string, string> WithProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-topic WITH(...) properties for Flink source tables (CREATE TABLE ... WITH (...)).
    /// Key is Kafka topic name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> SourceWithByTopic { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-topic WITH(...) properties for Flink sink tables (CREATE TABLE ... WITH (...)).
    /// Key is Kafka topic name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> SinkWithByTopic { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
