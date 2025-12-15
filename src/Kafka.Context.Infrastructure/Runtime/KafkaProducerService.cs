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
using System.Text;

namespace Kafka.Context.Infrastructure.Runtime;

internal static class KafkaProducerService
{
    public static async Task ProduceAsync<T>(KsqlDslOptions options, ILoggerFactory? loggerFactory, string topic, T entity, CancellationToken cancellationToken)
        => await ProduceAsync(options, loggerFactory, topic, entity, headers: null, cancellationToken).ConfigureAwait(false);

    public static async Task ProduceAsync<T>(KsqlDslOptions options, ILoggerFactory? loggerFactory, string topic, T entity, Dictionary<string, string>? headers, CancellationToken cancellationToken)
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

        ApplyTopicProducerConfig(options, topic, producerConfig);

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
        if (headers is not null && headers.Count > 0)
            message.Headers = CreateHeaders(headers);
        _ = await producer.ProduceAsync(topic, message).ConfigureAwait(false);
    }

    private static Headers CreateHeaders(Dictionary<string, string> headers)
    {
        var kafkaHeaders = new Headers();
        foreach (var kv in headers)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            var bytes = Encoding.UTF8.GetBytes(kv.Value ?? string.Empty);
            kafkaHeaders.Add(kv.Key, bytes);
        }
        return kafkaHeaders;
    }

    private static void ApplyTopicProducerConfig(KsqlDslOptions options, string topic, ProducerConfig producerConfig)
    {
        var section = ResolveTopicSection(options, topic);
        var producer = section?.Producer;
        if (producer is null)
            return;

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
            var attr = property.GetCustomAttribute<Kafka.Context.Attributes.KafkaDecimalAttribute>(inherit: true)
                       ?? throw new InvalidOperationException($"decimal field '{property.DeclaringType?.FullName}.{property.Name}' requires [KafkaDecimal].");

            var scale = attr.Scale;
            var unscaled = EncodeDecimalUnscaled(dec, scale);
            return new Avro.AvroDecimal(unscaled, scale);
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
