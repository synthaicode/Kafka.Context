using Kafka.Context.Configuration;

namespace Kafka.Context.Streaming.Flink;

public static class FlinkKafkaConnectorOptionsFactory
{
    public static FlinkKafkaConnectorOptions From(KsqlDslOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var kafka = new FlinkKafkaConnectorOptions
        {
            BootstrapServers = options.Common.BootstrapServers,
            SchemaRegistryUrl = options.SchemaRegistry.Url,
        };

        foreach (var (k, v) in options.Common.AdditionalProperties)
            kafka.AdditionalProperties[k] = v;

        foreach (var (k, v) in options.Streaming.Flink.With)
            kafka.WithProperties[k] = v;

        if (!string.IsNullOrWhiteSpace(options.Streaming.Flink.ScanStartupMode))
            AddOrValidate(kafka.WithProperties, "scan.startup.mode", options.Streaming.Flink.ScanStartupMode);

        foreach (var (topic, props) in options.Streaming.Flink.Sources)
            kafka.SourceWithByTopic[topic] = new Dictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);

        foreach (var (topic, groupId) in options.Streaming.Flink.SourceGroupIdByTopic)
        {
            if (!kafka.SourceWithByTopic.TryGetValue(topic, out var dict))
            {
                dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                kafka.SourceWithByTopic[topic] = dict;
            }

            AddOrValidate(dict, "properties.group.id", groupId);
        }

        foreach (var (topic, props) in options.Streaming.Flink.Sinks)
            kafka.SinkWithByTopic[topic] = new Dictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);

        return kafka;
    }

    private static void AddOrValidate(Dictionary<string, string> target, string key, string value)
    {
        if (target.TryAdd(key, value))
            return;

        if (string.Equals(target[key], value, StringComparison.Ordinal))
            return;

        throw new InvalidOperationException($"Duplicate WITH key '{key}' with different values.");
    }
}
