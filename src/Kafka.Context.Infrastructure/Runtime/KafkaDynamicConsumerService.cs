using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Kafka.Context.Configuration;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;

namespace Kafka.Context.Infrastructure.Runtime;

internal static class KafkaDynamicConsumerService
{
    public static async Task ForEachAsync(
        KsqlDslOptions options,
        ILoggerFactory? loggerFactory,
        string topic,
        Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action,
        bool autoCommit,
        Action<MessageMeta, Action> registerCommit,
        Func<string, int, long, string, Dictionary<string, string>, bool, Exception, Task>? onDecodeError,
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
            EnableAutoCommit = autoCommit,
        };

        foreach (var kvp in options.Common.AdditionalProperties)
            consumerConfig.Set(kvp.Key, kvp.Value);

        KafkaConsumerService.ApplyTopicConsumerConfig(options, topic, consumerConfig);

        using var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = options.SchemaRegistry.Url
        });

        var logger = loggerFactory?.CreateLogger("Kafka.Context.DynamicConsumer");

        var avro = new AvroDeserializer<GenericRecord>(schemaRegistry);

        using var consumer = new ConsumerBuilder<byte[], byte[]>(consumerConfig)
            .SetKeyDeserializer(Deserializers.ByteArray)
            .SetValueDeserializer(Deserializers.ByteArray)
            .Build();

        consumer.Subscribe(topic);

        await Task.Yield();

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<byte[], byte[]>? result = null;
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

            var headers = KafkaConsumerService.ExtractHeaders(result.Message.Headers);

            var timestampUtc = result.Message.Timestamp.Type == TimestampType.NotAvailable
                ? string.Empty
                : DateTimeOffset.FromUnixTimeMilliseconds(result.Message.Timestamp.UnixTimestampMs).UtcDateTime.ToString("O");

            var meta = new MessageMeta(
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                timestampUtc);

            object? keyObj;
            GenericRecord valueObj;
            try
            {
                keyObj = await DecodeKeyAsync(avro, topic, result.Message.Key).ConfigureAwait(false);
                valueObj = await DecodeValueAsync(avro, topic, result.Message.Value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (onDecodeError is not null)
                    await onDecodeError(result.Topic, result.Partition.Value, result.Offset.Value, timestampUtc, headers, autoCommit, ex).ConfigureAwait(false);

                consumer.Commit(result);
                continue;
            }

            if (!autoCommit)
                registerCommit(meta, () => consumer.Commit(result));

            await action(keyObj, valueObj, headers, meta).ConfigureAwait(false);
            logger?.LogDebug("Consumed {Topic} {Partition}:{Offset}", result.Topic, result.Partition.Value, result.Offset.Value);
        }

        consumer.Close();
    }

    private static async Task<object?> DecodeKeyAsync(AvroDeserializer<GenericRecord> avro, string topic, byte[]? keyBytes)
    {
        if (keyBytes is null)
            return null;

        if (!LooksLikeConfluentAvroPayload(keyBytes))
            return keyBytes;

        var ctx = new SerializationContext(MessageComponentType.Key, topic);
        try
        {
            return await avro.DeserializeAsync(keyBytes, isNull: false, ctx).ConfigureAwait(false);
        }
        catch
        {
            if (TryDecodeWindowedKey(avro, topic, keyBytes, out var windowed))
                return windowed;
            throw;
        }
    }

    private static async Task<GenericRecord> DecodeValueAsync(AvroDeserializer<GenericRecord> avro, string topic, byte[] valueBytes)
    {
        if (!LooksLikeConfluentAvroPayload(valueBytes))
            throw new InvalidOperationException("Value is not a Confluent Avro payload (magic byte missing).");

        var ctx = new SerializationContext(MessageComponentType.Value, topic);
        return await avro.DeserializeAsync(valueBytes, isNull: false, ctx).ConfigureAwait(false);
    }

    private static bool LooksLikeConfluentAvroPayload(byte[] bytes)
    {
        if (bytes.Length < 5) return false;
        if (bytes[0] != 0) return false;

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(1, 4));
        return schemaId > 0;
    }

    private static bool TryDecodeWindowedKey(AvroDeserializer<GenericRecord> avro, string topic, byte[] bytes, out WindowedKey windowedKey)
    {
        windowedKey = null!;

        var ctx = new SerializationContext(MessageComponentType.Key, topic);

        foreach (var suffixLen in new[] { 12, 16, 8 })
        {
            if (bytes.Length <= 5 + suffixLen)
                continue;

            var trimmed = bytes.AsSpan(0, bytes.Length - suffixLen).ToArray();
            if (!LooksLikeConfluentAvroPayload(trimmed))
                continue;

            try
            {
                var record = avro.DeserializeAsync(trimmed, isNull: false, ctx).GetAwaiter().GetResult();
                var suffix = bytes.AsSpan(bytes.Length - suffixLen, suffixLen);

                long start;
                long? end;
                int? seq;

                switch (suffixLen)
                {
                    case 12:
                        start = BinaryPrimitives.ReadInt64BigEndian(suffix.Slice(0, 8));
                        end = null;
                        seq = BinaryPrimitives.ReadInt32BigEndian(suffix.Slice(8, 4));
                        break;
                    case 16:
                        start = BinaryPrimitives.ReadInt64BigEndian(suffix.Slice(0, 8));
                        end = BinaryPrimitives.ReadInt64BigEndian(suffix.Slice(8, 8));
                        seq = null;
                        break;
                    case 8:
                        start = BinaryPrimitives.ReadInt64BigEndian(suffix);
                        end = null;
                        seq = null;
                        break;
                    default:
                        start = 0;
                        end = null;
                        seq = null;
                        break;
                }

                windowedKey = new WindowedKey(record, start, end, seq);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }
}
