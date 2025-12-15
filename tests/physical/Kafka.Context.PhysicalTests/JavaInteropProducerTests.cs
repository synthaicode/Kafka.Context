using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace Kafka.Context.PhysicalTests;

public sealed class JavaInteropProducerTests
{
    private const string BootstrapServers = "127.0.0.1:39092";
    private const string SchemaRegistryUrl = "http://127.0.0.1:18081";
    private const string SchemaRegistryContainerUrl = "http://localhost:8081";
    private const string KafkaContainerBootstrapServers = "kafka:29092";

    [Fact]
    public async Task JavaProducer_To_CSharp_DynamicTopicSet_Works()
    {
        if (!IsEnabled())
            return;

        await RunWithRetryAsync(async () =>
        {
            var topic = $"physical_java_console_{Guid.NewGuid():N}";

            await DeleteTopicsAsync(BootstrapServers, topic);
            await EnsureTopicExistsAsync(BootstrapServers, topic);

            var clientId = $"physical-java-{Guid.NewGuid():N}";
            var groupId = $"physical-java-group-{Guid.NewGuid():N}";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["KsqlDsl:Common:BootstrapServers"] = BootstrapServers,
                    ["KsqlDsl:Common:ClientId"] = clientId,
                    ["KsqlDsl:SchemaRegistry:Url"] = SchemaRegistryUrl,
                    [$"KsqlDsl:Topics:{topic}:Consumer:GroupId"] = groupId,
                    [$"KsqlDsl:Topics:{topic}:Consumer:AutoOffsetReset"] = "Earliest",
                })
                .Build();

            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            await using var ctx = new PhysicalDynamicContext(config, loggerFactory);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var got = new TaskCompletionSource<GenericRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

            var consumeTask = ctx.Topic(topic).ForEachAsync((_, value, _, _) =>
            {
                got.TrySetResult(value);
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token);

            var schemaJson = NormalizeJsonForArgument(BuildSchemaJson());
            await RunJavaProducerAsync(topic, schemaJson);

            var completed = await Task.WhenAny(got.Task, Task.Delay(TimeSpan.FromSeconds(50), CancellationToken.None));
            Assert.Equal(got.Task, completed);

            var record = await got.Task;

            Assert.True(ReadBool(record, "BoolValue"));
            Assert.Equal(123, ReadInt(record, "IntValue"));
            Assert.Equal(456L, ReadLong(record, "LongValue"));
            Assert.Equal("hello", ReadString(record, "TextValue"));

            // timestamp-millis may materialize as DateTime (logical conversion) or long (raw base type)
            var gotMillis = ReadTimestampMillis(record, "TimestampValue");
            var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.InRange(gotMillis, nowMillis - 120_000, nowMillis + 120_000);

            // decimal may materialize as AvroDecimal or byte[] depending on serializer/deserializer behavior.
            var amount = ReadDecimal(record, "Amount", scale: 2);
            Assert.Equal(12.34m, amount);

            var labels = ReadStringMap(record, "Labels");
            Assert.Equal("b", labels["a"]);

            await consumeTask;
        });
    }

    private static string BuildSchemaJson()
        => "{\"type\":\"record\",\"name\":\"JavaInteropAllTypes\",\"fields\":[{\"name\":\"BoolValue\",\"type\":\"boolean\"},{\"name\":\"IntValue\",\"type\":\"int\"},{\"name\":\"LongValue\",\"type\":\"long\"},{\"name\":\"TextValue\",\"type\":\"string\"},{\"name\":\"TimestampValue\",\"type\":{\"type\":\"long\",\"logicalType\":\"timestamp-millis\"}},{\"name\":\"Amount\",\"type\":{\"type\":\"bytes\",\"logicalType\":\"decimal\",\"precision\":9,\"scale\":2}},{\"name\":\"Labels\",\"type\":{\"type\":\"map\",\"values\":\"string\"}}]}";

    private static string NormalizeJsonForArgument(string json)
        => json.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();

    private static async Task RunJavaProducerAsync(string topic, string schemaJson)
    {
        var javaSource = $@"
import java.util.*;
import java.math.BigDecimal;
import java.nio.ByteBuffer;

import org.apache.avro.Schema;
import org.apache.avro.Conversions;
import org.apache.avro.LogicalType;
import org.apache.avro.generic.GenericData;
import org.apache.avro.generic.GenericRecord;

import org.apache.kafka.clients.producer.KafkaProducer;
import org.apache.kafka.clients.producer.ProducerRecord;

public final class KafkaContextInteropProducer {{
  public static void main(String[] args) throws Exception {{
    String topic = args[0];
    String schemaJson = args[1];

    Properties props = new Properties();
    props.put(""bootstrap.servers"", ""{KafkaContainerBootstrapServers}"");
    props.put(""acks"", ""all"");
    props.put(""enable.idempotence"", ""true"");
    props.put(""key.serializer"", ""org.apache.kafka.common.serialization.StringSerializer"");
    props.put(""value.serializer"", ""io.confluent.kafka.serializers.KafkaAvroSerializer"");
    props.put(""schema.registry.url"", ""{SchemaRegistryContainerUrl}"");

    KafkaProducer<String, Object> producer = new KafkaProducer<>(props);
    Schema schema = new Schema.Parser().parse(schemaJson);

    GenericRecord record = new GenericData.Record(schema);
    record.put(""BoolValue"", Boolean.TRUE);
    record.put(""IntValue"", 123);
    record.put(""LongValue"", 456L);
    record.put(""TextValue"", ""hello"");
    record.put(""TimestampValue"", System.currentTimeMillis());

    Schema amountSchema = schema.getField(""Amount"").schema();
    LogicalType amountLogical = amountSchema.getLogicalType();
    Conversions.DecimalConversion conv = new Conversions.DecimalConversion();
    ByteBuffer amountBytes = conv.toBytes(new BigDecimal(""12.34""), amountSchema, amountLogical);
    record.put(""Amount"", amountBytes);

    Map<String, String> labels = new HashMap<>();
    labels.put(""a"", ""b"");
    record.put(""Labels"", labels);

    producer.send(new ProducerRecord<>(topic, ""k1"", record)).get();
    producer.flush();
    producer.close();
  }}
}}
".Trim();

        var javaSourceB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(javaSource));
        var schemaB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(schemaJson));

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add("schema-registry");
        psi.ArgumentList.Add("bash");
        psi.ArgumentList.Add("-lc");

        var cmd = string.Join(" ", new[]
        {
            "set -euo pipefail;",
            $"echo '{javaSourceB64}' | base64 -d > /tmp/KafkaContextInteropProducer.java;",
            "CP=\"/usr/share/java/kafka-serde-tools/*:/usr/share/java/schema-registry/*:/usr/share/java/rest-utils/*:/usr/share/java/confluent-common/*:/usr/share/java/confluent-telemetry/*:/usr/share/java/schema-registry-plugins/*:/usr/share/java/confluent-security/schema-registry/*:/tmp\";",
            "javac -cp \"$CP\" -d /tmp /tmp/KafkaContextInteropProducer.java;",
            $"java -cp \"$CP\" KafkaContextInteropProducer '{topic}' \"$(echo '{schemaB64}' | base64 -d)\"",
        });

        psi.ArgumentList.Add(cmd);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start docker exec for Java producer");

        using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await p.WaitForExitAsync(exitCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        if (p.ExitCode != 0)
        {
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Java producer failed (exit={p.ExitCode}).\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }
    }

    private static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable("KAFKA_CONTEXT_PHYSICAL"), "1", StringComparison.Ordinal);

    private static async Task RunWithRetryAsync(Func<Task> action, int maxAttempts = 3)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attempt)));
                await Task.Delay(delay, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is OperationCanceledException)
                return true;
            if (cur is HttpRequestException)
                return true;
            if (cur is KafkaException)
                return true;
        }
        return false;
    }

    private static async Task DeleteTopicsAsync(string bootstrapServers, params string[] topics)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        try
        {
            await admin.DeleteTopicsAsync(topics).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static async Task EnsureTopicExistsAsync(string bootstrapServers, string topic)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        try
        {
            await admin.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }
            }).ConfigureAwait(false);
        }
        catch (CreateTopicsException)
        {
        }
    }

    private static bool ReadBool(GenericRecord record, string fieldName)
        => Convert.ToBoolean(record[FindFieldName(record, fieldName)], CultureInfo.InvariantCulture);

    private static int ReadInt(GenericRecord record, string fieldName)
        => Convert.ToInt32(record[FindFieldName(record, fieldName)], CultureInfo.InvariantCulture);

    private static long ReadLong(GenericRecord record, string fieldName)
        => Convert.ToInt64(record[FindFieldName(record, fieldName)], CultureInfo.InvariantCulture);

    private static string ReadString(GenericRecord record, string fieldName)
        => Convert.ToString(record[FindFieldName(record, fieldName)], CultureInfo.InvariantCulture) ?? string.Empty;

    private static long ReadTimestampMillis(GenericRecord record, string fieldName)
    {
        var v = record[FindFieldName(record, fieldName)];
        if (v is DateTime dt)
            return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
        if (v is DateTimeOffset dto)
            return dto.ToUnixTimeMilliseconds();
        return Convert.ToInt64(v, CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(GenericRecord record, string fieldName, int scale)
    {
        var v = record[FindFieldName(record, fieldName)];

        BigInteger unscaled;
        if (v is AvroDecimal avroDec)
            unscaled = avroDec.UnscaledValue;
        else if (v is byte[] bytes)
            unscaled = new BigInteger(bytes, isUnsigned: false, isBigEndian: true);
        else if (v is string s && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        else
            throw new InvalidOperationException($"Unexpected decimal value type: {v?.GetType().FullName ?? "<null>"}");

        var factor = 1m;
        for (var i = 0; i < scale; i++)
            factor *= 10m;
        return (decimal)unscaled / factor;
    }

    private static Dictionary<string, string> ReadStringMap(GenericRecord record, string fieldName)
    {
        var v = record[FindFieldName(record, fieldName)];
        if (v is IDictionary<string, string> mapStr)
            return new Dictionary<string, string>(mapStr, StringComparer.Ordinal);

        if (v is IDictionary<string, object?> mapObj)
        {
            var converted = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in mapObj)
                converted[kv.Key] = kv.Value?.ToString() ?? string.Empty;
            return converted;
        }

        if (v is object[] array)
        {
            var converted = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in array)
            {
                if (item is null) continue;

                if (item is KeyValuePair<string, string> kvStr)
                {
                    converted[kvStr.Key] = kvStr.Value;
                    continue;
                }

                if (item is KeyValuePair<string, object?> kvObj)
                {
                    converted[kvObj.Key] = kvObj.Value?.ToString() ?? string.Empty;
                    continue;
                }

                if (item is GenericRecord rec)
                {
                    var keyField = rec.Schema.Fields.FirstOrDefault(f => string.Equals(f.Name, "key", StringComparison.OrdinalIgnoreCase));
                    var valueField = rec.Schema.Fields.FirstOrDefault(f => string.Equals(f.Name, "value", StringComparison.OrdinalIgnoreCase));
                    if (keyField is null || valueField is null)
                        continue;

                    var k = rec[keyField.Name]?.ToString() ?? string.Empty;
                    var vv = rec[valueField.Name]?.ToString() ?? string.Empty;
                    converted[k] = vv;
                    continue;
                }
            }

            if (converted.Count > 0)
                return converted;
        }

        throw new InvalidOperationException($"Unexpected map type: {v?.GetType().FullName ?? "<null>"}");
    }

    private static string FindFieldName(GenericRecord record, string fieldName)
    {
        var f = record.Schema.Fields.FirstOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (f is null)
            throw new InvalidOperationException($"Field not found: {fieldName}. Available=[{string.Join(",", record.Schema.Fields.Select(x => x.Name))}]");
        return f.Name;
    }

    private sealed class PhysicalDynamicContext : KafkaContext
    {
        public PhysicalDynamicContext(IConfiguration configuration, ILoggerFactory loggerFactory)
            : base(configuration, loggerFactory) { }
    }
}
