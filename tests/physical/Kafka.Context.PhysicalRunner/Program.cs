using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var mode = args.Length > 0 ? args[0] : string.Empty;
if (!string.Equals(mode, "produce", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(mode, "consume", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: dotnet run -- [produce|consume]");
    return 2;
}

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_CONTEXT_BOOTSTRAP_SERVERS") ?? "127.0.0.1:39092";
var schemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_CONTEXT_SCHEMA_REGISTRY_URL") ?? "http://127.0.0.1:18081";
var clientId = Environment.GetEnvironmentVariable("KAFKA_CONTEXT_CLIENT_ID") ?? $"physical-runner-{Guid.NewGuid():N}";
var orderId = int.TryParse(Environment.GetEnvironmentVariable("KAFKA_CONTEXT_ORDER_ID"), out var id) ? id : 1;

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["KsqlDsl:Common:BootstrapServers"] = bootstrapServers,
        ["KsqlDsl:Common:ClientId"] = clientId,
        ["KsqlDsl:SchemaRegistry:Url"] = schemaRegistryUrl,
        ["KsqlDsl:DlqTopicName"] = $"dead_letter_queue_{clientId}",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:GroupId"] = $"{clientId}-orders-consumer-v1",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:AutoOffsetReset"] = "Earliest",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:AutoCommitIntervalMs"] = "5000",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:SessionTimeoutMs"] = "30000",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:HeartbeatIntervalMs"] = "3000",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:MaxPollIntervalMs"] = "300000",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:FetchMinBytes"] = "1",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:FetchMaxBytes"] = "52428800",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:IsolationLevel"] = "ReadUncommitted",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Consumer:AdditionalProperties:enable.partition.eof"] = "false",

        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:Acks"] = "All",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:CompressionType"] = "Snappy",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:EnableIdempotence"] = "true",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:MaxInFlightRequestsPerConnection"] = "1",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:LingerMs"] = "5",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:BatchSize"] = "16384",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:BatchNumMessages"] = "10000",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:DeliveryTimeoutMs"] = "120000",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:RetryBackoffMs"] = "100",
        ["KsqlDsl:Topics:physical_orders_physical_proc_xp:Producer:AdditionalProperties:message.max.bytes"] = "1000000",
    })
    .Build();

await using var ctx = new RunnerContext(config, LoggerFactory.Create(b => b.AddConsole()));

using (var provisionCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
{
    await ctx.ProvisionAsync(provisionCts.Token);
}

if (string.Equals(mode, "produce", StringComparison.OrdinalIgnoreCase))
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await ctx.Orders.AddAsync(new PhysicalOrder { Id = orderId, Amount = 10.5m }, cts.Token);
    Console.WriteLine($"PRODUCED id={orderId}");
    return 0;
}

{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var got = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    var task = ctx.Orders.ForEachAsync(o =>
    {
        if (o.Id == orderId)
        {
            Console.WriteLine($"CONSUMED id={o.Id}");
            got.TrySetResult(true);
            cts.Cancel();
        }
        return Task.CompletedTask;
    }, cts.Token);

    await Task.WhenAny(got.Task, Task.Delay(TimeSpan.FromSeconds(25), CancellationToken.None));
    if (!got.Task.IsCompletedSuccessfully)
    {
        Console.Error.WriteLine($"TIMEOUT waiting for id={orderId}");
        return 3;
    }

    await task;
    return 0;
}

[KafkaTopic("physical_orders_physical_proc_xp")]
public sealed class PhysicalOrder
{
    public int Id { get; set; }
    [KafkaDecimal(9, 2)]
    public decimal Amount { get; set; }
}

public sealed class RunnerContext : KafkaContext
{
    public RunnerContext(IConfiguration configuration, ILoggerFactory loggerFactory)
        : base(configuration, loggerFactory) { }

    public EventSet<PhysicalOrder> Orders { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PhysicalOrder>();
    }
}
