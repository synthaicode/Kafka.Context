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
        Action<object, Action> registerCommit,
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
            EnableAutoCommit = autoCommit,
            AutoOffsetReset = AutoOffsetReset.Latest,
        };

        foreach (var kvp in options.Common.AdditionalProperties)
            consumerConfig.Set(kvp.Key, kvp.Value);

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
            {
                registerCommit(entity!, () => consumer.Commit(result));
            }

            await action(entity, headers, meta).ConfigureAwait(false);
            logger?.LogDebug("Consumed {Topic} {Partition}:{Offset}", result.Topic, result.Partition.Value, result.Offset.Value);
        }

        consumer.Close();
    }

    private static Dictionary<string, string> ExtractHeaders(Headers? headers)
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
