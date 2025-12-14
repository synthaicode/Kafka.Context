using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErrorHandlingDlq;

[KsqlTopic("orders")]
public class Order
{
    public int Id { get; set; }

    [KsqlDecimal(precision: 18, scale: 2)]
    public decimal Amount { get; set; }
}

public class OrderContext : KafkaContext
{
    public OrderContext(IConfiguration configuration, ILoggerFactory? loggerFactory = null) : base(configuration, loggerFactory) { }
    public EventSet<Order> Orders { get; set; } = null!;
    protected override void OnModelCreating(IModelBuilder modelBuilder) => modelBuilder.Entity<Order>();
}

internal static class Program
{
    private static async Task Main()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build();
        await using var context = new OrderContext(configuration, LoggerFactory.Create(b => b.AddConsole()));

        await context.Orders
            .OnError(ErrorAction.DLQ)
            .WithRetry(3)
            .ForEachAsync(o =>
            {
                if (o.Amount < 0)
                    throw new InvalidOperationException("Amount cannot be negative");
                Console.WriteLine($"Processed order {o.Id}: {o.Amount}");
                return Task.CompletedTask;
            });
    }
}
