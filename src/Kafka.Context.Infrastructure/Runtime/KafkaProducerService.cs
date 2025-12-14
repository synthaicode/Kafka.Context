using Avro.Generic;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Kafka.Context.Configuration;
using Kafka.Context.Infrastructure.SchemaRegistry;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Numerics;
using System.Reflection;

namespace Kafka.Context.Infrastructure.Runtime;

internal static class KafkaProducerService
{
    public static async Task ProduceAsync<T>(KsqlDslOptions options, ILoggerFactory? loggerFactory, string topic, T entity, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("topic required", nameof(topic));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

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

        var valueSchemaJson = AvroSchemaBuilder.BuildValueSchema(typeof(T));
        var valueSchema = (Avro.RecordSchema)Avro.Schema.Parse(valueSchemaJson);

        var record = CreateRecord(valueSchema, entity);

        using var producer = new ProducerBuilder<Null, GenericRecord>(producerConfig)
            .SetValueSerializer(new AvroSerializer<GenericRecord>(schemaRegistry))
            .Build();

        var message = new Message<Null, GenericRecord> { Value = record };
        _ = await producer.ProduceAsync(topic, message).ConfigureAwait(false);
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

            var value = property.GetValue(entity);
            record.Add(field.Name, ConvertValue(property, value));
        }

        return record;
    }

    private static object? ConvertValue(PropertyInfo property, object? value)
    {
        if (value is null) return null;

        if (value is DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
        }

        if (value is decimal dec)
        {
            var attr = property.GetCustomAttribute<Kafka.Context.Attributes.KsqlDecimalAttribute>(inherit: true)
                       ?? throw new InvalidOperationException($"decimal field '{property.DeclaringType?.FullName}.{property.Name}' requires [KsqlDecimal].");

            var unscaled = EncodeDecimalUnscaled(dec, attr.Scale);
            return new Avro.AvroDecimal(unscaled, attr.Scale);
        }

        if (value is IDictionary<string, string> stringMap)
        {
            var avroMap = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in stringMap)
                avroMap[kv.Key] = kv.Value;
            return avroMap;
        }

        return value;
    }

    private static BigInteger EncodeDecimalUnscaled(decimal value, int scale)
    {
        var factor = 1m;
        for (var i = 0; i < scale; i++)
            factor *= 10m;

        var scaled = decimal.Round(value, scale, MidpointRounding.ToEven) * factor;
        if (scaled != decimal.Truncate(scaled))
            throw new InvalidOperationException($"decimal value '{value.ToString(CultureInfo.InvariantCulture)}' cannot be represented at scale {scale}.");

        return BigInteger.Parse(scaled.ToString("0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
