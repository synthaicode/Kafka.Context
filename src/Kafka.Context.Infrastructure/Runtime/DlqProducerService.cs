using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Kafka.Context.Configuration;
using Kafka.Context.Infrastructure.SchemaRegistry;
using Kafka.Context.Messaging;
using System.Reflection;

namespace Kafka.Context.Infrastructure.Runtime;

internal static class DlqProducerService
{
    public static async Task ProduceAsync(KsqlDslOptions options, string dlqTopicName, DlqEnvelope envelope, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(dlqTopicName)) throw new ArgumentException("dlqTopicName required", nameof(dlqTopicName));
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));

        cancellationToken.ThrowIfCancellationRequested();

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = options.Common.BootstrapServers,
            ClientId = options.Common.ClientId
        };

        foreach (var kvp in options.Common.AdditionalProperties)
            producerConfig.Set(kvp.Key, kvp.Value);

        ApplyTopicProducerConfig(options, dlqTopicName, producerConfig);

        using var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = options.SchemaRegistry.Url
        });

        if (options.SchemaRegistry.AutoRegisterSchemas)
            throw new InvalidOperationException("SchemaRegistry.AutoRegisterSchemas must be false. Register schemas before producing.");

        var keySchemaJson = AvroSchemaBuilder.BuildKeySchema(typeof(DlqEnvelope));
        var valueSchemaJson = AvroSchemaBuilder.BuildValueSchema(typeof(DlqEnvelope));

        var keySchema = (Avro.RecordSchema)Avro.Schema.Parse(keySchemaJson);
        var valueSchema = (Avro.RecordSchema)Avro.Schema.Parse(valueSchemaJson);

        var keyRecord = CreateRecordFromEnvelopeKey(keySchema, envelope);
        var valueRecord = CreateRecord(valueSchema, envelope);

        var serializerConfig = new AvroSerializerConfig
        {
            AutoRegisterSchemas = false,
        };

        using var producer = new ProducerBuilder<GenericRecord, GenericRecord>(producerConfig)
            .SetKeySerializer(new AvroSerializer<GenericRecord>(schemaRegistry, serializerConfig))
            .SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry, serializerConfig))
            .Build();

        var message = new Message<GenericRecord, GenericRecord> { Key = keyRecord, Value = valueRecord };
        _ = await producer.ProduceAsync(dlqTopicName, message).ConfigureAwait(false);
    }

    private static void ApplyTopicProducerConfig(KsqlDslOptions options, string topic, ProducerConfig producerConfig)
    {
        if (!options.Topics.TryGetValue(topic, out var section))
            return;

        var producer = section.Producer;
        if (!string.IsNullOrWhiteSpace(producer.Acks) && TryParseAcks(producer.Acks, out var acks))
            producerConfig.Acks = acks;

        if (!string.IsNullOrWhiteSpace(producer.CompressionType) && TryParseCompressionType(producer.CompressionType, out var comp))
            producerConfig.CompressionType = comp;

        if (producer.EnableIdempotence is not null)
            producerConfig.EnableIdempotence = producer.EnableIdempotence.Value;

        if (producer.MaxInFlightRequestsPerConnection > 0)
            producerConfig.MaxInFlight = producer.MaxInFlightRequestsPerConnection;

        if (producer.LingerMs > 0)
            producerConfig.LingerMs = producer.LingerMs;

        if (producer.BatchSize > 0)
            producerConfig.BatchSize = producer.BatchSize;

        if (producer.BatchNumMessages > 0)
            producerConfig.BatchNumMessages = producer.BatchNumMessages;

        if (producer.DeliveryTimeoutMs > 0)
            producerConfig.MessageTimeoutMs = producer.DeliveryTimeoutMs;

        if (producer.RetryBackoffMs > 0)
            producerConfig.RetryBackoffMs = producer.RetryBackoffMs;

        foreach (var kv in producer.AdditionalProperties)
            producerConfig.Set(kv.Key, kv.Value);
    }

    private static bool TryParseAcks(string value, out Acks acks)
    {
        if (string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
        {
            acks = Acks.All;
            return true;
        }
        if (string.Equals(value, "Leader", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
        {
            acks = Acks.Leader;
            return true;
        }
        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
        {
            acks = Acks.None;
            return true;
        }

        acks = Acks.All;
        return false;
    }

    private static bool TryParseCompressionType(string value, out CompressionType compression)
    {
        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
        {
            compression = CompressionType.None;
            return true;
        }
        if (string.Equals(value, "Gzip", StringComparison.OrdinalIgnoreCase))
        {
            compression = CompressionType.Gzip;
            return true;
        }
        if (string.Equals(value, "Snappy", StringComparison.OrdinalIgnoreCase))
        {
            compression = CompressionType.Snappy;
            return true;
        }
        if (string.Equals(value, "Lz4", StringComparison.OrdinalIgnoreCase))
        {
            compression = CompressionType.Lz4;
            return true;
        }
        if (string.Equals(value, "Zstd", StringComparison.OrdinalIgnoreCase))
        {
            compression = CompressionType.Zstd;
            return true;
        }

        compression = CompressionType.None;
        return false;
    }

    private static GenericRecord CreateRecordFromEnvelopeKey(Avro.RecordSchema schema, DlqEnvelope envelope)
    {
        var record = new GenericRecord(schema);
        foreach (var field in schema.Fields)
        {
            var property = typeof(DlqEnvelope).GetProperty(field.Name, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
                throw new InvalidOperationException($"Missing key property '{typeof(DlqEnvelope).FullName}.{field.Name}'.");
            record.Add(field.Name, property.GetValue(envelope));
        }
        return record;
    }

    private static GenericRecord CreateRecord<T>(Avro.RecordSchema schema, T entity)
    {
        var record = new GenericRecord(schema);
        var entityType = typeof(T);
        foreach (var field in schema.Fields)
        {
            var property = entityType.GetProperty(field.Name, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
                throw new InvalidOperationException($"Missing property '{entityType.FullName}.{field.Name}' required by schema.");
            record.Add(field.Name, ConvertValue(property.GetValue(entity)));
        }
        return record;
    }

    private static object? ConvertValue(object? value)
    {
        if (value is null) return null;

        if (value is IDictionary<string, string> stringMap)
        {
            var avroMap = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in stringMap)
                avroMap[kv.Key] = kv.Value;
            return avroMap;
        }

        return value;
    }
}
