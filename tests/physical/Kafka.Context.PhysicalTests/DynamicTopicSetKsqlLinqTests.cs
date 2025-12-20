using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Ksql.Linq;
using Ksql.Linq.Configuration;
using Ksql.Linq.Core.Abstractions;
using Ksql.Linq.Core.Attributes;
using Ksql.Linq.Core.Modeling;
using Kafka.Context.Messaging;
using Avro.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kafka.Context.PhysicalTests;

public sealed class DynamicTopicSetKsqlLinqTests
{
    private const string InputTopic = "physical_ksqllinq_transactions";
    private const string OutputTopicBase = "physical_ksqllinq_user_stats";

    [Fact]
    public async Task DynamicTopicSet_Consumes_KsqlLinq_Hopping_WindowedKey()
    {
        if (!IsEnabled())
            return;

        await RunWithRetryAsync(async () =>
        {
            var outputTopic = $"{OutputTopicBase}_{Guid.NewGuid():N}";
            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var inputStreamName = $"S_TX_{suffix}";
            var outputTableName = $"T_STATS_{suffix}";

            await DeleteTopicsAsync("127.0.0.1:39092", InputTopic, outputTopic);
            await EnsureTopicExistsAsync("127.0.0.1:39092", InputTopic);

            await using (var ksql = new KsqlLinqProducerContext(clientId: $"ksqllinq-{Guid.NewGuid():N}"))
            {
                await EnsureKsqlObjectsAsync(ksql, inputStreamName, outputTableName, outputTopic);

                var clientId = $"physical-dyn-ksqllinq-{Guid.NewGuid():N}";
                var groupId = $"physical-dyn-ksqllinq-group-{Guid.NewGuid():N}";

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["KsqlDsl:Common:BootstrapServers"] = "127.0.0.1:39092",
                        ["KsqlDsl:Common:ClientId"] = clientId,
                        ["KsqlDsl:SchemaRegistry:Url"] = "http://127.0.0.1:18081",
                        ["KsqlDsl:DlqTopicName"] = $"dead_letter_queue_{clientId}",
                        [$"KsqlDsl:Topics:{outputTopic}:Consumer:GroupId"] = groupId,
                        [$"KsqlDsl:Topics:{outputTopic}:Consumer:AutoOffsetReset"] = "Earliest",
                    })
                    .Build();

                var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
                await using var ctx = new PhysicalDynamicContext(config, loggerFactory);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var got = new TaskCompletionSource<(object? Key, GenericRecord Value)>(TaskCreationOptions.RunContinuationsAsynchronously);
                var printed = 0;

                var consumeTask = ctx.Topic(outputTopic).ForEachAsync((key, value, _, meta) =>
                {
                    if (meta.Topic != outputTopic)
                        return Task.CompletedTask;

                    if (printed++ < 3)
                        Console.WriteLine($"[DynamicTopicSet] keyType={key?.GetType().FullName ?? "<null>"} valueFields=[{string.Join(",", value.Schema.Fields.Select(f => f.Name))}]");

                    AssertContainsField(value, "TransactionCount");
                    got.TrySetResult((key, value));
                    cts.Cancel();
                    return Task.CompletedTask;
                }, cts.Token);

                var tx = ksql.Set<Transaction>();
                var now = DateTime.UtcNow;

                // Ensure ksqlDB has had a chance to start the persistent query before producing.
                await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);

                var userId = $"user_{Guid.NewGuid():N}";
                await tx.AddAsync(new Transaction
                {
                    TransactionId = $"t-{Guid.NewGuid():N}",
                    UserId = userId,
                    Amount = 100,
                    Currency = "USD",
                    TransactionTime = now
                });

                var completed = await Task.WhenAny(got.Task, Task.Delay(TimeSpan.FromSeconds(80), CancellationToken.None));
                Assert.Equal(got.Task, completed);

                var (gotKey, gotValue) = await got.Task;
                Assert.NotNull(gotValue);

                if (gotKey is WindowedKey wk)
                {
                    Assert.True(wk.WindowStartUnixMs > 0);
                    AssertContainsField(wk.Key, "UserId");
                    AssertContainsField(wk.Key, "Currency");
                    Assert.True(RecordContainsString(wk.Key, userId));
                }
                else if (gotKey is GenericRecord keyRecord)
                {
                    AssertContainsField(keyRecord, "UserId");
                    AssertContainsField(keyRecord, "Currency");
                    Assert.True(RecordContainsString(keyRecord, userId));
                    TryAssertHasWindowBoundsInKey(keyRecord);
                }
                else
                {
                    throw new Xunit.Sdk.XunitException($"Unexpected key type: {gotKey?.GetType().FullName ?? "<null>"}");
                }

                await consumeTask;
            }
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
        }
        return false;
    }

    private static async Task EnsureKsqlObjectsAsync(KsqlContext context, string inputStreamName, string outputTableName, string outputTopic)
    {
        // Ensure Kafka Streams starts from the beginning of the (recreated) input topic.
        await context.ExecuteStatementAsync("SET 'auto.offset.reset'='earliest';").ConfigureAwait(false);

        foreach (var stmt in new[]
        {
            $@"
CREATE STREAM IF NOT EXISTS {inputStreamName} (
  UserId STRING,
  Amount DOUBLE,
  Currency STRING,
  TransactionTime TIMESTAMP
) WITH (
  KAFKA_TOPIC='{InputTopic}',
  VALUE_FORMAT='AVRO',
  TIMESTAMP='TransactionTime'
);".Trim(),

            $@"
CREATE TABLE IF NOT EXISTS {outputTableName}
  WITH (
    KAFKA_TOPIC='{outputTopic}',
    KEY_FORMAT='AVRO',
    VALUE_FORMAT='AVRO'
  ) AS
  SELECT
    UserId,
    Currency,
    COUNT(*) AS TransactionCount
  FROM {inputStreamName}
  WINDOW HOPPING (SIZE 5 MINUTES, ADVANCE BY 1 MINUTES)
  GROUP BY UserId, Currency
  EMIT CHANGES;".Trim()
        })
        {
            await ExecuteKsqlStatementAsync(context, stmt).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteKsqlStatementAsync(KsqlContext context, string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return;

        var normalized = NormalizeCreateOrReplace(statement);
        Console.WriteLine($"[Ksql.Linq] Execute: {normalized}");
        await context.ExecuteStatementAsync(normalized).ConfigureAwait(false);
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
                new TopicSpecification
                {
                    Name = topic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            }).ConfigureAwait(false);
        }
        catch (CreateTopicsException)
        {
        }
    }

    private static string NormalizeCreateOrReplace(string statement)
    {
        if (statement.StartsWith("CREATE TABLE IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase))
            return "CREATE OR REPLACE TABLE " + statement["CREATE TABLE IF NOT EXISTS ".Length..];
        if (statement.StartsWith("CREATE STREAM IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase))
            return "CREATE OR REPLACE STREAM " + statement["CREATE STREAM IF NOT EXISTS ".Length..];
        if (statement.StartsWith("CREATE STREAM ", StringComparison.OrdinalIgnoreCase))
            return "CREATE OR REPLACE STREAM " + statement["CREATE STREAM ".Length..];
        return statement;
    }

    private static bool RecordContainsString(GenericRecord record, string expected)
    {
        foreach (var f in record.Schema.Fields)
        {
            var v = record[f.Name];
            if (v is string s && string.Equals(s, expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void TryAssertHasWindowBoundsInKey(GenericRecord key)
    {
        var fields = key.Schema.Fields.Select(f => f.Name).ToList();
        var start = fields.FirstOrDefault(n => n.Contains("WINDOW", StringComparison.OrdinalIgnoreCase) && n.Contains("START", StringComparison.OrdinalIgnoreCase));
        var end = fields.FirstOrDefault(n => n.Contains("WINDOW", StringComparison.OrdinalIgnoreCase) && n.Contains("END", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return;

        _ = (long)key[start!];
        _ = (long)key[end!];
    }

    // Note: ksqlDB windowed keys often encode window boundaries as trailing bytes on the key payload.
    // Those boundaries are surfaced via `WindowedKey` when they are not embedded as Avro fields.

    private static void AssertContainsField(GenericRecord record, string fieldName)
    {
        Assert.Contains(record.Schema.Fields, f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class PhysicalDynamicContext : KafkaContext
    {
        public PhysicalDynamicContext(IConfiguration configuration, ILoggerFactory loggerFactory)
            : base(configuration, loggerFactory) { }
    }

    [KsqlTopic(InputTopic)]
    private sealed class Transaction
    {
        [KsqlKey(1)] public string TransactionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public double Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        [KsqlTimestamp] public DateTime TransactionTime { get; set; }
    }

    private sealed class KsqlLinqProducerContext : KsqlContext
    {
        public KsqlLinqProducerContext(string clientId)
            : base(new KsqlDslOptions
            {
                Common = new CommonSection { BootstrapServers = "127.0.0.1:39092", ClientId = clientId },
                SchemaRegistry = new Ksql.Linq.Core.Configuration.SchemaRegistrySection { Url = "http://127.0.0.1:18081" },
                KsqlDbUrl = "http://127.0.0.1:18088"
            })
        { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Transaction>();
        }
    }
}
