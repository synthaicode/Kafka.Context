using Avro;
using Avro.Generic;
using Confluent.Kafka.Admin;
using Confluent.Kafka;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Ksql.Linq;
using Ksql.Linq.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Numerics;

namespace Kafka.Context.PhysicalTests;

public sealed class DynamicTopicSetKsqlLinqAllClrTypesTests
{
    private const string BootstrapServers = "127.0.0.1:39092";
    private const string SchemaRegistryUrl = "http://127.0.0.1:18081";
    private const string KsqlDbUrl = "http://127.0.0.1:18088";

    private const string InputTopic = "physical_ksqllinq_allclrtypes_in";

    [Fact]
    public async Task DynamicTopicSet_Consumes_KsqlDb_Passthrough_AllSupportedClrTypes()
    {
        if (!IsEnabled())
            return;

        await RunWithRetryAsync(async () =>
        {
            var outputTopic = $"physical_ksqllinq_allclrtypes_out_{Guid.NewGuid():N}";
            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var inputStreamName = $"S_TYPES_IN_{suffix}";
            var outputStreamName = $"S_TYPES_OUT_{suffix}";

            await DeleteTopicsAsync(BootstrapServers, InputTopic, outputTopic);

            var clientId = $"physical-alltypes-{Guid.NewGuid():N}";
            var groupId = $"physical-alltypes-group-{Guid.NewGuid():N}";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["KsqlDsl:Common:BootstrapServers"] = BootstrapServers,
                    ["KsqlDsl:Common:ClientId"] = clientId,
                    ["KsqlDsl:SchemaRegistry:Url"] = SchemaRegistryUrl,
                    ["KsqlDsl:DlqTopicName"] = $"dead_letter_queue_{clientId}",
                    [$"KsqlDsl:Topics:{outputTopic}:Consumer:GroupId"] = groupId,
                    [$"KsqlDsl:Topics:{outputTopic}:Consumer:AutoOffsetReset"] = "Earliest",
                })
                .Build();

            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

            await using var producerCtx = new AllClrTypesProducerContext(config, loggerFactory);
            using (var provisionCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                await producerCtx.ProvisionAsync(provisionCts.Token);

            await EnsureKsqlObjectsAsync(inputStreamName, outputStreamName, outputTopic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var got = new TaskCompletionSource<GenericRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var consumerCtx = new PhysicalDynamicContext(config, loggerFactory);
            var consumeTask = consumerCtx.Topic(outputTopic).ForEachAsync((_, value, _, _) =>
            {
                got.TrySetResult(value);
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token);

            var now = DateTime.UtcNow;
            var expected = new AllClrTypesValue
            {
                BoolValue = true,
                IntValue = 123,
                LongValue = 456L,
                TextValue = "hello",
                TimestampValue = now,
                Amount = 12.34m,
                Labels = new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "b" },
                NullableInt = null,
                NullableLong = 999,
                NullableBool = null,
                NullableText = null,
                NullableTimestamp = null,
                NullableAmount = null,
                NullableLabels = null,
            };

            for (var i = 0; i < 5 && !got.Task.IsCompleted; i++)
            {
                await producerCtx.AllTypes.AddAsync(expected, CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
            }

            var completed = await Task.WhenAny(got.Task, Task.Delay(TimeSpan.FromSeconds(80), CancellationToken.None));
            Assert.Equal(got.Task, completed);

            var record = await got.Task;

            Assert.Equal(expected.BoolValue, ReadBool(record, nameof(AllClrTypesValue.BoolValue)));
            Assert.Equal(expected.IntValue, ReadInt(record, nameof(AllClrTypesValue.IntValue)));
            Assert.Equal(expected.LongValue, ReadLong(record, nameof(AllClrTypesValue.LongValue)));
            Assert.Equal(expected.TextValue, ReadString(record, nameof(AllClrTypesValue.TextValue)));

            var gotMillis = ReadTimestampMillis(record, nameof(AllClrTypesValue.TimestampValue));
            var expectedMillis = new DateTimeOffset(expected.TimestampValue).ToUnixTimeMilliseconds();
            Assert.InRange(gotMillis, expectedMillis - 60_000, expectedMillis + 60_000);

            Assert.Equal(expected.Amount, ReadDecimal(record, nameof(AllClrTypesValue.Amount), scale: 2));

            var labels = ReadStringMap(record, nameof(AllClrTypesValue.Labels));
            Assert.Equal("b", labels["a"]);

            Assert.Null(ReadNullable(record, nameof(AllClrTypesValue.NullableInt)));
            Assert.Equal(999, ReadInt(record, nameof(AllClrTypesValue.NullableLong)));
            Assert.Null(ReadNullable(record, nameof(AllClrTypesValue.NullableBool)));
            Assert.Null(ReadNullable(record, nameof(AllClrTypesValue.NullableText)));
            Assert.Null(ReadNullable(record, nameof(AllClrTypesValue.NullableTimestamp)));
            Assert.Null(ReadNullable(record, nameof(AllClrTypesValue.NullableAmount)));
            Assert.Null(ReadNullable(record, nameof(AllClrTypesValue.NullableLabels)));

            await consumeTask;
        });
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

    private static async Task EnsureKsqlObjectsAsync(string inputStreamName, string outputStreamName, string outputTopic)
    {
        await using var ksql = new KsqlAdminContext(clientId: $"ksqllinq-admin-{Guid.NewGuid():N}");

        await ksql.ExecuteStatementAsync("SET 'auto.offset.reset'='earliest';").ConfigureAwait(false);

        foreach (var stmt in new[]
        {
            $@"
CREATE STREAM IF NOT EXISTS {inputStreamName} (
  BoolValue BOOLEAN,
  IntValue INTEGER,
  LongValue BIGINT,
  TextValue STRING,
  TimestampValue TIMESTAMP,
  Amount DECIMAL(9,2),
  Labels MAP<STRING, STRING>,
  NullableInt INTEGER,
  NullableLong BIGINT,
  NullableBool BOOLEAN,
  NullableText STRING,
  NullableTimestamp TIMESTAMP,
  NullableAmount DECIMAL(9,2),
  NullableLabels MAP<STRING, STRING>
) WITH (
  KAFKA_TOPIC='{InputTopic}',
  VALUE_FORMAT='AVRO'
);".Trim(),

            $@"
CREATE STREAM IF NOT EXISTS {outputStreamName}
  WITH (
    KAFKA_TOPIC='{outputTopic}',
    VALUE_FORMAT='AVRO'
  ) AS
  SELECT
    BoolValue,
    IntValue,
    LongValue,
    TextValue,
    TimestampValue,
    Amount,
    Labels,
    NullableInt,
    NullableLong,
    NullableBool,
    NullableText,
    NullableTimestamp,
    NullableAmount,
    NullableLabels
  FROM {inputStreamName}
  EMIT CHANGES;".Trim()
        })
        {
            await ksql.ExecuteStatementAsync(stmt).ConfigureAwait(false);
        }

        await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
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

    private static object? ReadNullable(GenericRecord record, string fieldName)
        => record[FindFieldName(record, fieldName)];

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
        else
            unscaled = new BigInteger(Convert.ToInt64(v, CultureInfo.InvariantCulture));

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

            if (array.Length == 2 && array[0] is string singleKey)
            {
                converted[singleKey] = array[1]?.ToString() ?? string.Empty;
                return converted;
            }

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

    private sealed class KsqlAdminContext : KsqlContext
    {
        public KsqlAdminContext(string clientId)
            : base(new KsqlDslOptions
            {
                Common = new CommonSection { BootstrapServers = BootstrapServers, ClientId = clientId },
                SchemaRegistry = new Ksql.Linq.Core.Configuration.SchemaRegistrySection { Url = SchemaRegistryUrl },
                KsqlDbUrl = KsqlDbUrl
            })
        { }

        protected override void OnModelCreating(Ksql.Linq.Core.Abstractions.IModelBuilder modelBuilder) { }
    }

    [KafkaTopic(InputTopic)]
    private sealed class AllClrTypesValue
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

        public int? NullableInt { get; set; }
        public long? NullableLong { get; set; }
        public bool? NullableBool { get; set; }
        public string? NullableText { get; set; }

        [KafkaTimestamp]
        public DateTime? NullableTimestamp { get; set; }

        [KafkaDecimal(9, 2)]
        public decimal? NullableAmount { get; set; }

        public Dictionary<string, string>? NullableLabels { get; set; }
    }

    private sealed class AllClrTypesProducerContext : KafkaContext
    {
        public AllClrTypesProducerContext(IConfiguration configuration, ILoggerFactory loggerFactory)
            : base(configuration, loggerFactory) { }

        public EventSet<AllClrTypesValue> AllTypes { get; set; } = null!;

        protected override void OnModelCreating(Kafka.Context.Abstractions.IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllClrTypesValue>();
        }
    }
}
