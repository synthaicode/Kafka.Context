using System.ComponentModel;

namespace Kafka.Context.Configuration;

public sealed class KsqlDslOptions
{
    public CommonOptions Common { get; set; } = new();
    public SchemaRegistryOptions SchemaRegistry { get; set; } = new();

    public string DlqTopicName { get; set; } = "dead_letter_queue";

    public Dictionary<string, TopicSection> Topics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [DefaultValue(true)]
    public bool AdjustReplicationFactorToBrokerCount { get; set; } = true;
}

public sealed class CommonOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string ClientId { get; set; } = "kafka-context";

    public int MetadataMaxAgeMs { get; set; } = 0;
    public Dictionary<string, string> AdditionalProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SchemaRegistryOptions
{
    public string Url { get; set; } = string.Empty;
    public SchemaRegistryMonitoringOptions Monitoring { get; set; } = new();
}

public sealed class SchemaRegistryMonitoringOptions
{
    public TimeSpan SubjectWaitTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public int SubjectWaitMaxAttempts { get; set; } = 12;
}

public sealed class TopicSection
{
    public TopicCreationSection Creation { get; set; } = new();
    public TopicConsumerSection Consumer { get; set; } = new();
    public TopicProducerSection Producer { get; set; } = new();
}

public sealed class TopicCreationSection
{
    public int NumPartitions { get; set; } = 0;
    public short ReplicationFactor { get; set; } = 0;
    public Dictionary<string, string> Configs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TopicConsumerSection
{
    public string GroupId { get; set; } = string.Empty;
    public string AutoOffsetReset { get; set; } = string.Empty;
    public int AutoCommitIntervalMs { get; set; } = 0;
    public int SessionTimeoutMs { get; set; } = 0;
    public int HeartbeatIntervalMs { get; set; } = 0;
    public int MaxPollIntervalMs { get; set; } = 0;
    public int FetchMinBytes { get; set; } = 0;
    public int FetchMaxBytes { get; set; } = 0;
    public string IsolationLevel { get; set; } = string.Empty;

    public Dictionary<string, string> AdditionalProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TopicProducerSection
{
    public string Acks { get; set; } = string.Empty;
    public string CompressionType { get; set; } = string.Empty;
    public bool? EnableIdempotence { get; set; } = null;
    public int MaxInFlightRequestsPerConnection { get; set; } = 0;
    public int LingerMs { get; set; } = 0;
    public int BatchSize { get; set; } = 0;
    public int BatchNumMessages { get; set; } = 0;
    public int DeliveryTimeoutMs { get; set; } = 0;
    public int RetryBackoffMs { get; set; } = 0;

    public Dictionary<string, string> AdditionalProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
