using Avro.Generic;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Diagnostics;

namespace Kafka.Context.PhysicalTests;

public sealed class SchemaEvolutionPhysicalTests
{
    private const string BootstrapServers = "127.0.0.1:39092";
    private const string SchemaRegistryUrl = "http://127.0.0.1:18081";
    private const string Topic = "physical_schema_evolution";
    private const string SchemaRegistryContainerUrl = "http://localhost:8081";
    private const string KafkaContainerBootstrapServers = "kafka:29092";

    [Fact]
    public async Task SchemaEvolution_AddFieldWithDefault_IsCompatible_And_OldPocoCanConsumeNewData()
    {
        if (!IsEnabled())
            return;

        await RunWithRetryAsync(async () =>
        {
            var topic = Topic;
            var subject = $"{topic}-value"; // TopicNameStrategy

            await DeleteTopicsAsync(BootstrapServers, topic);
            await EnsureTopicExistsAsync(BootstrapServers, topic);

            using var sr = new SchemaRegistryRestClient(new Uri(SchemaRegistryUrl));

            // Register V1, then evolve to V2 under an explicit compatibility rule.
            await sr.DeleteSubjectAsync(subject);
            await sr.SetSubjectCompatibilityAsync(subject, "FULL");
            Console.WriteLine($"[SR] subject strategy=TopicNameStrategy subject={subject} compatibility=FULL");

            var v1Schema = BuildV1SchemaJson();
            var v2Schema = BuildV2SchemaJson();

            var v1Id = await sr.RegisterSchemaAsync(subject, v1Schema);
            var v2Id = await sr.RegisterSchemaAsync(subject, v2Schema);

            var latest = await sr.GetLatestAsync(subject);
            Console.WriteLine($"[SR] subject={subject} compatibility=FULL latestVersion={latest.Version} id={latest.Id}");
            Console.WriteLine($"[SR] v1Id={v1Id} v2Id={v2Id}");

            Assert.Equal(v2Id, latest.Id);
            Assert.True(latest.Version >= 2);

            var clientId = $"physical-evo-{Guid.NewGuid():N}";
            var groupId = $"physical-evo-group-{Guid.NewGuid():N}";

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
            await using var ctx = new EvolutionContext(config, loggerFactory);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var got = new TaskCompletionSource<(EvolutionV1 Value, MessageMeta Meta)>(TaskCreationOptions.RunContinuationsAsynchronously);

            var consumeTask = ctx.Events.ForEachAsync((v, _, meta) =>
            {
                Console.WriteLine($"[Consume] topic={meta.Topic} partition={meta.Partition} offset={meta.Offset} ts={meta.TimestampUtc}");
                got.TrySetResult((v, meta));
                cts.Cancel();
                return Task.CompletedTask;
            }, autoCommit: true, cancellationToken: cts.Token);

            // Produce a V2 record from the JVM side (KafkaAvroSerializer, TopicNameStrategy).
            Console.WriteLine("[Produce] java=container schema-registry serializer=KafkaAvroSerializer subject=TopicNameStrategy auto.register.schemas=false");
            await RunJavaProducerAsync(topic, v2Schema, autoRegisterSchemas: false);

            var completed = await Task.WhenAny(got.Task, Task.Delay(TimeSpan.FromSeconds(50), CancellationToken.None));
            Assert.Equal(got.Task, completed);

            var (value, _) = await got.Task;
            Assert.True(value.BoolValue);
            Assert.Equal(123, value.IntValue);
            Assert.Equal(456L, value.LongValue);
            Assert.Equal("hello", value.TextValue);
            Assert.Equal(12.34m, value.Amount);
            Assert.Equal("b", value.Labels["a"]);

            await consumeTask;
        });
    }

    private static string BuildV1SchemaJson()
        => "{\"type\":\"record\",\"name\":\"EvolutionRecord\",\"namespace\":\"Kafka.Context.PhysicalTests\",\"fields\":[{\"name\":\"BoolValue\",\"type\":\"boolean\"},{\"name\":\"IntValue\",\"type\":\"int\"},{\"name\":\"LongValue\",\"type\":\"long\"},{\"name\":\"TextValue\",\"type\":\"string\"},{\"name\":\"TimestampValue\",\"type\":{\"type\":\"long\",\"logicalType\":\"timestamp-millis\"}},{\"name\":\"Amount\",\"type\":{\"type\":\"bytes\",\"logicalType\":\"decimal\",\"precision\":9,\"scale\":2}},{\"name\":\"Labels\",\"type\":{\"type\":\"map\",\"values\":\"string\"}}]}";

    private static string BuildV2SchemaJson()
        => "{\"type\":\"record\",\"name\":\"EvolutionRecord\",\"namespace\":\"Kafka.Context.PhysicalTests\",\"fields\":[{\"name\":\"BoolValue\",\"type\":\"boolean\"},{\"name\":\"IntValue\",\"type\":\"int\"},{\"name\":\"LongValue\",\"type\":\"long\"},{\"name\":\"TextValue\",\"type\":\"string\"},{\"name\":\"TimestampValue\",\"type\":{\"type\":\"long\",\"logicalType\":\"timestamp-millis\"}},{\"name\":\"Amount\",\"type\":{\"type\":\"bytes\",\"logicalType\":\"decimal\",\"precision\":9,\"scale\":2}},{\"name\":\"Labels\",\"type\":{\"type\":\"map\",\"values\":\"string\"}},{\"name\":\"NewField\",\"type\":\"string\",\"default\":\"v2\"}]}";

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

    private sealed class EvolutionContext : KafkaContext
    {
        public EvolutionContext(IConfiguration configuration, ILoggerFactory loggerFactory)
            : base(configuration, loggerFactory) { }

        public EventSet<EvolutionV1> Events { get; set; } = null!;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EvolutionV1>();
        }
    }

    [KafkaTopic(Topic)]
    private sealed class EvolutionV1
    {
        public bool BoolValue { get; set; }
        public int IntValue { get; set; }
        public long LongValue { get; set; }
        public string TextValue { get; set; } = string.Empty;

        [KafkaTimestamp]
        public DateTime TimestampValue { get; set; }

        [KafkaDecimal(9, 2)]
        public decimal Amount { get; set; }

        public Dictionary<string, string> Labels { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class SchemaRegistryRestClient : IDisposable
    {
        private readonly HttpClient _http;

        public SchemaRegistryRestClient(Uri baseUri)
        {
            _http = new HttpClient { BaseAddress = baseUri };
        }

        public async Task SetSubjectCompatibilityAsync(string subject, string compatibility)
        {
            var resp = await _http.PutAsJsonAsync($"/config/{Uri.EscapeDataString(subject)}", new { compatibility }).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
        }

        public async Task DeleteSubjectAsync(string subject)
        {
            try
            {
                var resp = await _http.DeleteAsync($"/subjects/{Uri.EscapeDataString(subject)}").ConfigureAwait(false);
                // 404 is fine (nothing to delete).
                if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.NotFound)
                    resp.EnsureSuccessStatusCode();
            }
            catch
            {
            }
        }

        public async Task<int> RegisterSchemaAsync(string subject, string schemaJson)
        {
            var resp = await _http.PostAsJsonAsync($"/subjects/{Uri.EscapeDataString(subject)}/versions", new { schema = schemaJson, schemaType = "AVRO" }).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>().ConfigureAwait(false);
            return body?.Id ?? throw new InvalidOperationException("Schema Registry register response missing id.");
        }

        public async Task<LatestResponse> GetLatestAsync(string subject)
        {
            var resp = await _http.GetAsync($"/subjects/{Uri.EscapeDataString(subject)}/versions/latest").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<LatestResponse>().ConfigureAwait(false);
            return body ?? throw new InvalidOperationException("Schema Registry latest response missing.");
        }

        public void Dispose() => _http.Dispose();

        public sealed record RegisterResponse(int Id);
        public sealed record LatestResponse(string Subject, int Version, int Id, string Schema);
    }

    private static async Task RunJavaProducerAsync(string topic, string schemaJson, bool autoRegisterSchemas)
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

public final class KafkaContextInteropEvolutionProducer {{
  public static void main(String[] args) throws Exception {{
    String topic = args[0];
    String schemaJson = args[1];

    Properties props = new Properties();
    props.put(""bootstrap.servers"", ""{KafkaContainerBootstrapServers}"");
    props.put(""acks"", ""all"");
    props.put(""enable.idempotence"", ""true"");
    props.put(""auto.register.schemas"", ""{autoRegisterSchemas.ToString().ToLowerInvariant()}"");
    props.put(""key.serializer"", ""org.apache.kafka.common.serialization.StringSerializer"");
    props.put(""value.serializer"", ""io.confluent.kafka.serializers.KafkaAvroSerializer"");
    props.put(""schema.registry.url"", ""{SchemaRegistryContainerUrl}"");
    props.put(""key.subject.name.strategy"", ""io.confluent.kafka.serializers.subject.TopicNameStrategy"");
    props.put(""value.subject.name.strategy"", ""io.confluent.kafka.serializers.subject.TopicNameStrategy"");

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

    if (schema.getField(""NewField"") != null) {{
      record.put(""NewField"", ""v2"");
    }}

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
            $"echo '{javaSourceB64}' | base64 -d > /tmp/KafkaContextInteropEvolutionProducer.java;",
            "CP=\"/usr/share/java/kafka-serde-tools/*:/usr/share/java/schema-registry/*:/usr/share/java/rest-utils/*:/usr/share/java/confluent-common/*:/usr/share/java/confluent-telemetry/*:/usr/share/java/schema-registry-plugins/*:/usr/share/java/confluent-security/schema-registry/*:/tmp\";",
            "javac -cp \"$CP\" -d /tmp /tmp/KafkaContextInteropEvolutionProducer.java;",
            $"java -cp \"$CP\" KafkaContextInteropEvolutionProducer '{topic}' \"$(echo '{schemaB64}' | base64 -d)\"",
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
}
