using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Kafka.Context.Configuration;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Kafka.Context.Infrastructure.Runtime;

internal static class KafkaConsumerService
{
    public static async Task ForEachAsync<T>(
        KsqlDslOptions options,
        ILoggerFactory? loggerFactory,
        string topic,
        Func<T, Dictionary<string, string>, MessageMeta, Task> action,
        bool autoCommit,
        Action<MessageMeta, Action> registerCommit,
        Func<string, int, long, string, Dictionary<string, string>, bool, Exception, Task>? onMappingError,
        CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("topic required", nameof(topic));
        if (action is null) throw new ArgumentNullException(nameof(action));

        cancellationToken.ThrowIfCancellationRequested();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = options.Common.BootstrapServers,
            GroupId = $"{options.Common.ClientId}-{topic}",
            AutoOffsetReset = AutoOffsetReset.Latest,
        };

        foreach (var kvp in options.Common.AdditionalProperties)
            consumerConfig.Set(kvp.Key, kvp.Value);

        ApplyTopicConsumerConfig(options, topic, consumerConfig);
        consumerConfig.EnableAutoCommit = autoCommit;

        using var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = options.SchemaRegistry.Url
        });

        var logger = loggerFactory?.CreateLogger("Kafka.Context.Consumer");

        using var consumer = new ConsumerBuilder<Ignore, GenericRecord>(consumerConfig)
            .SetValueDeserializer(new AvroDeserializer<GenericRecord>(schemaRegistry).AsSyncOverAsync())
            .Build();

        consumer.Subscribe(topic);

        await Task.Yield();

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<Ignore, GenericRecord>? result = null;
            try
            {
                result = consumer.Consume(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result?.Message?.Value is null)
                continue;

            var headers = ExtractHeaders(result.Message.Headers);

            var timestampUtc = result.Message.Timestamp.Type == TimestampType.NotAvailable
                ? string.Empty
                : DateTimeOffset.FromUnixTimeMilliseconds(result.Message.Timestamp.UnixTimestampMs).UtcDateTime.ToString("O");

            var meta = new MessageMeta(
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                timestampUtc);

            T entity;
            try
            {
                entity = AvroGenericMapper.MapTo<T>(result.Message.Value);
            }
            catch (Exception ex)
            {
                if (onMappingError is not null)
                    await onMappingError(result.Topic, result.Partition.Value, result.Offset.Value, timestampUtc, headers, autoCommit, ex).ConfigureAwait(false);

                consumer.Commit(result);
                continue;
            }

            if (!autoCommit)
                registerCommit(meta, () => consumer.Commit(result));

            await action(entity, headers, meta).ConfigureAwait(false);
            logger?.LogDebug("Consumed {Topic} {Partition}:{Offset}", result.Topic, result.Partition.Value, result.Offset.Value);
        }

        consumer.Close();
    }

    internal static void ApplyTopicConsumerConfig(KsqlDslOptions options, string topic, ConsumerConfig consumerConfig)
    {
        var section = ResolveTopicSection(options, topic);
        var consumer = section?.Consumer;
        if (consumer is null)
            return;

        if (!string.IsNullOrWhiteSpace(consumer.GroupId))
            consumerConfig.GroupId = consumer.GroupId;

        if (!string.IsNullOrWhiteSpace(consumer.AutoOffsetReset) &&
            TryParseAutoOffsetReset(consumer.AutoOffsetReset, out var aor))
        {
            consumerConfig.AutoOffsetReset = aor;
        }

        if (consumer.AutoCommitIntervalMs > 0)
            consumerConfig.AutoCommitIntervalMs = consumer.AutoCommitIntervalMs;
        if (consumer.SessionTimeoutMs > 0)
            consumerConfig.SessionTimeoutMs = consumer.SessionTimeoutMs;
        if (consumer.HeartbeatIntervalMs > 0)
            consumerConfig.HeartbeatIntervalMs = consumer.HeartbeatIntervalMs;
        if (consumer.MaxPollIntervalMs > 0)
            consumerConfig.MaxPollIntervalMs = consumer.MaxPollIntervalMs;
        if (consumer.FetchMinBytes > 0)
            consumerConfig.FetchMinBytes = consumer.FetchMinBytes;
        if (consumer.FetchMaxBytes > 0)
            consumerConfig.FetchMaxBytes = consumer.FetchMaxBytes;

        if (!string.IsNullOrWhiteSpace(consumer.IsolationLevel) &&
            TryParseIsolationLevel(consumer.IsolationLevel, out var iso))
        {
            consumerConfig.IsolationLevel = iso;
        }

        foreach (var kv in consumer.AdditionalProperties)
            consumerConfig.Set(kv.Key, kv.Value);
    }

    private static TopicSection? ResolveTopicSection(KsqlDslOptions options, string topicName)
    {
        if (options.Topics.TryGetValue(topicName, out var section))
            return section;

        var baseKey = topicName;
        if (topicName.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) ||
            topicName.EndsWith(".int", StringComparison.OrdinalIgnoreCase))
        {
            baseKey = topicName[..^4];
            if (options.Topics.TryGetValue(baseKey, out section))
                return section;
        }

        var lookup = baseKey;
        if (lookup.Contains("_hb_", StringComparison.OrdinalIgnoreCase))
            lookup = lookup.Replace("_hb_", "_", StringComparison.OrdinalIgnoreCase);

        return FindByPrefix(lookup, options);
    }

    private static TopicSection? FindByPrefix(string name, KsqlDslOptions options)
    {
        if (options.Topics.TryGetValue(name, out var section))
            return section;

        var idx = name.LastIndexOf('_');
        while (idx > 0)
        {
            var prefix = name[..idx];
            if (options.Topics.TryGetValue(prefix, out section))
                return section;
            idx = prefix.LastIndexOf('_');
        }

        return null;
    }

    private static bool TryParseAutoOffsetReset(string value, out AutoOffsetReset aor)
    {
        if (string.Equals(value, "Earliest", StringComparison.OrdinalIgnoreCase))
        {
            aor = AutoOffsetReset.Earliest;
            return true;
        }
        if (string.Equals(value, "Latest", StringComparison.OrdinalIgnoreCase))
        {
            aor = AutoOffsetReset.Latest;
            return true;
        }
        if (string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase))
        {
            aor = AutoOffsetReset.Error;
            return true;
        }

        aor = AutoOffsetReset.Latest;
        return false;
    }

    private static bool TryParseIsolationLevel(string value, out IsolationLevel iso)
    {
        if (string.Equals(value, "ReadCommitted", StringComparison.OrdinalIgnoreCase))
        {
            iso = IsolationLevel.ReadCommitted;
            return true;
        }
        if (string.Equals(value, "ReadUncommitted", StringComparison.OrdinalIgnoreCase))
        {
            iso = IsolationLevel.ReadUncommitted;
            return true;
        }

        iso = IsolationLevel.ReadUncommitted;
        return false;
    }

    internal static Dictionary<string, string> ExtractHeaders(Headers? headers)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null) return dict;

        foreach (var header in headers)
        {
            if (header.GetValueBytes() is not { } bytes)
                continue;

            dict[header.Key] = Encoding.UTF8.GetString(bytes);
        }

        return dict;
    }
}
