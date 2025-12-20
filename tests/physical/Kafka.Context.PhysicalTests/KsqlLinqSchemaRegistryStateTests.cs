using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.SchemaRegistry;
using Ksql.Linq;
using Ksql.Linq.Configuration;
using Ksql.Linq.Core.Abstractions;

namespace Kafka.Context.PhysicalTests;

public sealed class KsqlLinqSchemaRegistryStateTests
{
    private const string BootstrapServers = "127.0.0.1:39092";
    private const string SchemaRegistryUrl = "http://127.0.0.1:18081";
    private const string InputTopic = "physical_ksqllinq_sr_state_in";

    [Fact]
    public async Task KsqlLinq_Can_Create_SchemaRegistry_Subjects_From_KsqlDb()
    {
        if (!IsEnabled())
            return;

        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var outputTopicV1 = $"physical_ksqllinq_sr_state_out_v1_{suffix}";
        var outputTopicV2 = $"physical_ksqllinq_sr_state_out_v2_{suffix}";

        var inputStreamName = $"S_SR_STATE_IN_{suffix}";
        var outputTableNameV1 = $"T_SR_STATE_OUT_V1_{suffix}";
        var outputTableNameV2 = $"T_SR_STATE_OUT_V2_{suffix}";

        Console.WriteLine($"[Ksql.Linq/SR] inputTopic={InputTopic} outputTopicV1={outputTopicV1} outputTopicV2={outputTopicV2}");

        await DeleteTopicsAsync(BootstrapServers, InputTopic, outputTopicV1, outputTopicV2);
        await EnsureTopicExistsAsync(BootstrapServers, InputTopic);

        await using (var ksql = new KsqlLinqStateContext(clientId: $"ksqllinq-sr-state-{Guid.NewGuid():N}"))
        {
            await EnsureKsqlObjectsV1Async(ksql, inputStreamName, outputTableNameV1, InputTopic, outputTopicV1);

            // Ensure ksqlDB produces at least one output record.
            await ProduceInputRecordAsync(ksql, "v1");
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);

            using var sr = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = SchemaRegistryUrl });

            var subjectV1 = $"{outputTopicV1}-value";
            var v1 = await sr.GetLatestSchemaAsync(subjectV1);
            Console.WriteLine($"[SR] subject={subjectV1} id={v1.Id} version={v1.Version}");
            Assert.Contains("TransactionCount", v1.SchemaString, StringComparison.OrdinalIgnoreCase);

            await EnsureKsqlObjectsV2Async(ksql, outputTableNameV2, inputStreamName, outputTopicV2);
            await ProduceInputRecordAsync(ksql, "v2");
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);

            var subjectV2 = $"{outputTopicV2}-value";
            var v2 = await sr.GetLatestSchemaAsync(subjectV2);
            Console.WriteLine($"[SR] subject={subjectV2} id={v2.Id} version={v2.Version}");
            Assert.Contains("LatestCurrency", v2.SchemaString, StringComparison.OrdinalIgnoreCase);

            Assert.NotEqual(subjectV1, subjectV2);
        }
    }

    private static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable("KAFKA_CONTEXT_PHYSICAL"), "1", StringComparison.Ordinal);

    private static async Task EnsureKsqlObjectsV1Async(
        KsqlContext context,
        string inputStreamName,
        string outputTableName,
        string inputTopic,
        string outputTopic)
    {
        await context.ExecuteStatementAsync("SET 'auto.offset.reset'='earliest';").ConfigureAwait(false);

        foreach (var stmt in new[]
        {
            $@"
CREATE OR REPLACE STREAM {inputStreamName} (
  UserId STRING,
  Amount DOUBLE,
  Currency STRING,
  TransactionTime TIMESTAMP
) WITH (
  KAFKA_TOPIC='{inputTopic}',
  VALUE_FORMAT='AVRO',
  TIMESTAMP='TransactionTime'
);".Trim(),

            $@"
CREATE OR REPLACE TABLE {outputTableName}
  WITH (
    KAFKA_TOPIC='{outputTopic}',
    KEY_FORMAT='AVRO',
    VALUE_FORMAT='AVRO'
  ) AS
  SELECT
    UserId,
    COUNT(*) AS TransactionCount
  FROM {inputStreamName}
  GROUP BY UserId
  EMIT CHANGES;".Trim()
        })
        {
            await ExecuteKsqlStatementAsync(context, stmt).ConfigureAwait(false);
        }
    }

    private static async Task EnsureKsqlObjectsV2Async(
        KsqlContext context,
        string outputTableName,
        string inputStreamName,
        string outputTopic)
    {
        var stmt = $@"
CREATE OR REPLACE TABLE {outputTableName}
  WITH (
    KAFKA_TOPIC='{outputTopic}',
    KEY_FORMAT='AVRO',
    VALUE_FORMAT='AVRO'
  ) AS
  SELECT
    UserId,
    COUNT(*) AS TransactionCount,
    LATEST_BY_OFFSET(Currency) AS LatestCurrency
  FROM {inputStreamName}
  GROUP BY UserId
  EMIT CHANGES;".Trim();

        await ExecuteKsqlStatementAsync(context, stmt).ConfigureAwait(false);
    }

    private static async Task ExecuteKsqlStatementAsync(KsqlContext context, string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return;

        Console.WriteLine($"[Ksql.Linq] Execute: {statement.ReplaceLineEndings(" ")}");
        await context.ExecuteStatementAsync(statement).ConfigureAwait(false);
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

    private sealed class KsqlLinqStateContext : KsqlContext
    {
        public KsqlLinqStateContext(string clientId)
            : base(new KsqlDslOptions
            {
                Common = new CommonSection { BootstrapServers = BootstrapServers, ClientId = clientId },
                SchemaRegistry = new Ksql.Linq.Core.Configuration.SchemaRegistrySection { Url = SchemaRegistryUrl },
                KsqlDbUrl = "http://127.0.0.1:18088"
            })
        {
        }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Transaction>();
        }
    }

    [Ksql.Linq.Core.Attributes.KsqlTopic(InputTopic)]
    private sealed class Transaction
    {
        public string UserId { get; set; } = string.Empty;
        public double Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        [Ksql.Linq.Core.Attributes.KsqlTimestamp] public DateTime TransactionTime { get; set; }
    }

    private static async Task ProduceInputRecordAsync(KsqlLinqStateContext context, string label)
    {
        var userId = $"user_{label}_{Guid.NewGuid():N}";
        var tx = context.Set<Transaction>();
        await tx.AddAsync(new Transaction
        {
            UserId = userId,
            Amount = 10,
            Currency = "USD",
            TransactionTime = DateTime.UtcNow
        });
    }
}
