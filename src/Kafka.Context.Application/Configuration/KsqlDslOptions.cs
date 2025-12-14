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
}

public sealed class TopicCreationSection
{
    public int NumPartitions { get; set; } = 0;
    public short ReplicationFactor { get; set; } = 0;
    public Dictionary<string, string> Configs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

