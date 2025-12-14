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

        using var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = options.SchemaRegistry.Url
        });

        var keySchemaJson = AvroSchemaBuilder.BuildKeySchema(typeof(DlqEnvelope));
        var valueSchemaJson = AvroSchemaBuilder.BuildValueSchema(typeof(DlqEnvelope));

        var keySchema = (Avro.RecordSchema)Avro.Schema.Parse(keySchemaJson);
        var valueSchema = (Avro.RecordSchema)Avro.Schema.Parse(valueSchemaJson);

        var keyRecord = CreateRecordFromEnvelopeKey(keySchema, envelope);
        var valueRecord = CreateRecord(valueSchema, envelope);

        using var producer = new ProducerBuilder<GenericRecord, GenericRecord>(producerConfig)
            .SetKeySerializer(new AvroSerializer<GenericRecord>(schemaRegistry))
            .SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry))
            .Build();

        var message = new Message<GenericRecord, GenericRecord> { Key = keyRecord, Value = valueRecord };
        _ = await producer.ProduceAsync(dlqTopicName, message).ConfigureAwait(false);
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
