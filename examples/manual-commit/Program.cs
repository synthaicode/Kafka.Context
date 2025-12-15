using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ManualCommit;

[KafkaTopic("manual-commit-orders")]
public class ManualCommitOrder
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}

public class ManualCommitContext : KafkaContext
{
    public ManualCommitContext(IConfiguration configuration, ILoggerFactory? loggerFactory = null) : base(configuration, loggerFactory) { }
    public EventSet<ManualCommitOrder> Orders { get; set; } = null!;
    protected override void OnModelCreating(IModelBuilder modelBuilder) => modelBuilder.Entity<ManualCommitOrder>();
}

internal static class Program
{
    private static async Task Main()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build();
        await using var context = new ManualCommitContext(configuration, LoggerFactory.Create(b => b.AddConsole()));

        await context.Orders.AddAsync(new ManualCommitOrder { OrderId = 1, Amount = 10m }, new Dictionary<string, string>
        {
            ["traceId"] = Guid.NewGuid().ToString("N"),
            ["source"] = "manual-commit",
        });

         await context.Orders.ForEachAsync((order, headers, meta) =>
         {
             Console.WriteLine($"Processing order {order.OrderId}: {order.Amount} (traceId={headers.GetValueOrDefault("traceId", "-")})");
            context.Orders.Commit(meta);
             return Task.CompletedTask;
         }, autoCommit: false);
     }
}
