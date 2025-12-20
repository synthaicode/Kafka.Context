using Confluent.Kafka.Admin;
using Confluent.Kafka;
using Ksql.Linq;
using Ksql.Linq.Configuration;
using Ksql.Linq.Core.Attributes;
using Kafka.Context.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KafkaModelBuilder = Kafka.Context.Abstractions.IModelBuilder;
using KsqlModelBuilder = Ksql.Linq.Core.Abstractions.IModelBuilder;

namespace Kafka.Context.PhysicalTests
{

public sealed class KsqlDbPocoConsumePhysicalTests
{
    private const string BootstrapServers = "127.0.0.1:39092";
    private const string SchemaRegistryUrl = "http://127.0.0.1:18081";
    private const string KsqlDbUrl = "http://127.0.0.1:18088";

    private const string InputTopic = "physical_cli_poco_in";
    private const string OutputTopic = "physical_cli_poco_out";

    [Fact]
    public async Task KafkaContext_Can_Consume_ksqlDB_Output_As_POCO()
    {
        if (!IsEnabled())
            return;

        await DeleteTopicsAsync(BootstrapServers, InputTopic, OutputTopic);
        await EnsureTopicExistsAsync(BootstrapServers, InputTopic);

        var clientId = $"physical-cli-poco-{Guid.NewGuid():N}";
        var groupId = $"physical-cli-poco-group-{Guid.NewGuid():N}";

        await using (var ksql = new KsqlLinqContext(clientId: $"ksqllinq-cli-poco-{Guid.NewGuid():N}"))
        {
            await EnsureKsqlObjectsAsync(ksql);

            var tx = ksql.Set<Transaction>();
            await tx.AddAsync(new Transaction
            {
                UserId = $"user_{Guid.NewGuid():N}",
                Amount = 10,
                Currency = "USD",
                TransactionTime = DateTime.UtcNow
            });
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KsqlDsl:Common:BootstrapServers"] = BootstrapServers,
                ["KsqlDsl:Common:ClientId"] = clientId,
                ["KsqlDsl:SchemaRegistry:Url"] = SchemaRegistryUrl,
                ["KsqlDsl:DlqTopicName"] = $"dead_letter_queue_{clientId}",
                [$"KsqlDsl:Topics:{OutputTopic}:Consumer:GroupId"] = groupId,
                [$"KsqlDsl:Topics:{OutputTopic}:Consumer:AutoOffsetReset"] = "Earliest",
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

        await using var ctx = new PocoConsumeContext(config, loggerFactory);
        using (var provisionCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
        {
            await ctx.ProvisionAsync(provisionCts.Token);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var got = new TaskCompletionSource<io.confluent.ksql.avro_schemas.KsqlDataSourceSchema>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumeTask = ctx.Stats.ForEachAsync((row, _, meta) =>
        {
            if (meta.Topic != OutputTopic)
                return Task.CompletedTask;

            got.TrySetResult(row);
            cts.Cancel();
            return Task.CompletedTask;
        }, autoCommit: true, cancellationToken: cts.Token);

        var completed = await Task.WhenAny(got.Task, Task.Delay(TimeSpan.FromSeconds(55), CancellationToken.None));
        Assert.Equal(got.Task, completed);

        var record = await got.Task;
        Assert.NotNull(record);
        Assert.True(record.TRANSACTIONCOUNT is null or >= 0);
        Assert.True(record.LATESTCURRENCY is null or "USD");

        await consumeTask;
    }

    private static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable("KAFKA_CONTEXT_PHYSICAL"), "1", StringComparison.Ordinal);

    private static async Task EnsureKsqlObjectsAsync(KsqlContext context)
    {
        await context.ExecuteStatementAsync("SET 'auto.offset.reset'='earliest';");

        foreach (var stmt in new[]
        {
            $@"
CREATE OR REPLACE STREAM S_CLI_POCO_IN (
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
CREATE OR REPLACE TABLE T_CLI_POCO_OUT
  WITH (
    KAFKA_TOPIC='{OutputTopic}',
    KEY_FORMAT='AVRO',
    VALUE_FORMAT='AVRO'
  ) AS
  SELECT
    UserId,
    COUNT(*) AS TransactionCount,
    LATEST_BY_OFFSET(Currency) AS LatestCurrency
  FROM S_CLI_POCO_IN
  GROUP BY UserId
  EMIT CHANGES;".Trim()
        })
        {
            await context.ExecuteStatementAsync(stmt);
        }
    }

    private static async Task DeleteTopicsAsync(string bootstrapServers, params string[] topics)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        try
        {
            await admin.DeleteTopicsAsync(topics);
            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
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
            });
        }
        catch (CreateTopicsException)
        {
        }
    }

    [KsqlTopic(InputTopic)]
    private sealed class Transaction
    {
        public string UserId { get; set; } = string.Empty;
        public double Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        [KsqlTimestamp] public DateTime TransactionTime { get; set; }
    }

    private sealed class KsqlLinqContext : KsqlContext
    {
        public KsqlLinqContext(string clientId)
            : base(new KsqlDslOptions
            {
                Common = new CommonSection { BootstrapServers = BootstrapServers, ClientId = clientId },
                SchemaRegistry = new Ksql.Linq.Core.Configuration.SchemaRegistrySection { Url = SchemaRegistryUrl },
                KsqlDbUrl = KsqlDbUrl
            })
        { }

        protected override void OnModelCreating(KsqlModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Transaction>();
        }
    }

    private sealed class PocoConsumeContext : KafkaContext
    {
        public PocoConsumeContext(IConfiguration configuration, ILoggerFactory loggerFactory)
            : base(configuration, loggerFactory) { }

        public EventSet<io.confluent.ksql.avro_schemas.KsqlDataSourceSchema> Stats { get; set; } = null!;

        protected override void OnModelCreating(KafkaModelBuilder modelBuilder)
        {
            modelBuilder.Entity<io.confluent.ksql.avro_schemas.KsqlDataSourceSchema>();
        }
    }
}

}

namespace io.confluent.ksql.avro_schemas
{
    [KafkaTopic("physical_cli_poco_out")]
    public sealed class KsqlDataSourceSchema
    {
        public long? TRANSACTIONCOUNT { get; set; }
        public string? LATESTCURRENCY { get; set; }
    }
}
