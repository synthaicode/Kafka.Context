using Kafka.Context.Attributes;
using Kafka.Context.Configuration;
using System.Reflection;

namespace Kafka.Context.Infrastructure.Admin;

internal sealed record TopicExpectation(
    string Name,
    int NumPartitions,
    short ReplicationFactor,
    Dictionary<string, string> Configs,
    bool EnforceMatch)
{
    public static TopicExpectation ForEntityType(Type entityType, KsqlDslOptions options)
    {
        if (entityType is null) throw new ArgumentNullException(nameof(entityType));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var topicAttr = entityType.GetCustomAttribute<KsqlTopicAttribute>(inherit: true)
            ?? throw new InvalidOperationException($"Missing [{nameof(KsqlTopicAttribute)}] on entity type '{entityType.FullName}'.");

        var topicName = topicAttr.Name;
        var appCreation = ResolveCreation(topicName, options);

        var partitions = topicAttr.PartitionCount;
        var rf = topicAttr.ReplicationFactor;
        var configs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var enforceMatch = false;

        if (appCreation is not null)
        {
            if (appCreation.NumPartitions > 0)
            {
                partitions = appCreation.NumPartitions;
                enforceMatch = true;
            }

            if (appCreation.ReplicationFactor > 0)
            {
                rf = appCreation.ReplicationFactor;
                enforceMatch = true;
            }

            if (appCreation.Configs.Count > 0)
            {
                foreach (var kv in appCreation.Configs)
                    configs[kv.Key] = kv.Value;
                enforceMatch = true;
            }
        }

        return new TopicExpectation(topicName, partitions, rf, configs, enforceMatch);
    }

    public static TopicExpectation ForDlq(string topicName, KsqlDslOptions options)
    {
        if (string.IsNullOrWhiteSpace(topicName)) throw new ArgumentException("DLQ topic name required", nameof(topicName));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var configs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cleanup.policy"] = "delete",
            ["retention.ms"] = "5000"
        };

        var partitions = 1;
        short rf = 1;

        var appCreation = ResolveCreation(topicName, options);
        if (appCreation is not null)
        {
            if (appCreation.NumPartitions > 0) partitions = appCreation.NumPartitions;
            if (appCreation.ReplicationFactor > 0) rf = appCreation.ReplicationFactor;
            foreach (var kv in appCreation.Configs)
                configs[kv.Key] = kv.Value;
        }

        return new TopicExpectation(topicName, partitions, rf, configs, EnforceMatch: true);
    }

    private static TopicCreationSection? ResolveCreation(string topicName, KsqlDslOptions options)
    {
        if (options.Topics.TryGetValue(topicName, out var explicitSection))
        {
            if (topicName.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) ||
                topicName.EndsWith(".int", StringComparison.OrdinalIgnoreCase))
            {
                if (explicitSection.Creation.NumPartitions > 0 ||
                    explicitSection.Creation.ReplicationFactor > 0 ||
                    explicitSection.Creation.Configs.Count > 0)
                {
                    var baseTopic = topicName[..^4];
                    throw new InvalidOperationException($"Structure settings must be defined on base topic '{baseTopic}'");
                }
            }

            return explicitSection.Creation;
        }

        var baseKey = topicName;
        var hasPubIntSuffix = false;
        if (topicName.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) ||
            topicName.EndsWith(".int", StringComparison.OrdinalIgnoreCase))
        {
            baseKey = topicName[..^4];
            hasPubIntSuffix = true;
        }

        var lookup = baseKey;
        if (lookup.Contains("_hb_", StringComparison.OrdinalIgnoreCase))
            lookup = lookup.Replace("_hb_", "_", StringComparison.OrdinalIgnoreCase);

        var creation = FindCreationByPrefix(lookup, options);
        if (hasPubIntSuffix && creation is null)
            throw new InvalidOperationException($"Base topic '{baseKey}' missing creation settings for '{topicName}'");

        return creation;
    }

    private static TopicCreationSection? FindCreationByPrefix(string name, KsqlDslOptions options)
    {
        if (options.Topics.TryGetValue(name, out var section) && HasAnySetting(section.Creation))
            return section.Creation;

        var idx = name.LastIndexOf('_');
        while (idx > 0)
        {
            var prefix = name[..idx];
            if (options.Topics.TryGetValue(prefix, out section) && HasAnySetting(section.Creation))
                return section.Creation;
            idx = prefix.LastIndexOf('_');
        }

        return null;
    }

    private static bool HasAnySetting(TopicCreationSection creation)
        => creation.NumPartitions > 0 || creation.ReplicationFactor > 0 || creation.Configs.Count > 0;
}
