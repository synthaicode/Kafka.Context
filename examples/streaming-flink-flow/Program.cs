using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Kafka.Context.Configuration;
using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlinkStreamingFlowExample;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

        var run = args.Contains("--run", StringComparer.OrdinalIgnoreCase);

        await using var ctx = new DemoContext(configuration, loggerFactory, run);

        // (optional) Kafka topic + SR provisioning
        // await ctx.ProvisionAsync();

        // Streaming provisioning (Flink DDL)
        await ctx.ProvisionStreamingAsync();

        if (!run) return;

        // Produce to input topics
        await ctx.Orders.AddAsync(new Order { Id = 1, CustomerId = 10, Amount = 100m });
        await ctx.Customers.AddAsync(new Customer { Id = 10, Name = "alice", IsActive = true });

        // Consume from derived output topic (short timeout)
        await ctx.OrderSummaries.ForEachAsync((row, headers, meta) =>
        {
            Console.WriteLine($"OrderSummary: CustomerId={row.CustomerId} CustomerName={row.CustomerName}");
            Console.WriteLine($"Meta: {meta.Topic} {meta.Partition}:{meta.Offset} {meta.TimestampUtc}");
            return Task.CompletedTask;
        }, timeout: TimeSpan.FromSeconds(5));
    }
}

internal sealed class DemoContext : KafkaContext
{
    private readonly bool _run;

    public DemoContext(IConfiguration configuration, ILoggerFactory loggerFactory, bool run)
        : base(configuration, loggerFactory)
    {
        _run = run;
    }

    public EventSet<Order> Orders { get; private set; } = null!;
    public EventSet<Customer> Customers { get; private set; } = null!;
    public EventSet<OrderSummary> OrderSummaries { get; private set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        modelBuilder.Entity<Customer>();

        modelBuilder.Entity<OrderByCustomer>().ToQuery(q => q
            .From<Order>()
            .GroupBy(o => o.CustomerId)
            .Select(o => new OrderByCustomer { CustomerId = o.CustomerId }));

        modelBuilder.Entity<OrderSummary>().ToQuery(q => q
            .From<OrderByCustomer>()
            .Join<OrderByCustomer, Customer>((x, c) => x.CustomerId == c.Id)
            .Where((x, c) => c.IsActive)
            .Select((x, c) => new OrderSummary { CustomerId = x.CustomerId, CustomerName = c.Name }));
    }

    protected override IStreamingDialectProvider ResolveStreamingDialectProvider()
    {
        var opts = Configuration.GetKsqlDslOptions();
        var kafka = FlinkKafkaConnectorOptionsFactory.From(opts);

        return new FlinkDialectProvider(kafka, (ddl, ct) =>
        {
            Console.WriteLine("---- Flink DDL ----");
            Console.WriteLine(ddl);

            if (!_run)
            {
                Console.WriteLine("(dry-run: DDL is not executed)");
                return Task.CompletedTask;
            }

            // TODO: replace with SQL Gateway / REST executor.
            Console.WriteLine("(run: TODO execute DDL on Flink)");
            return Task.CompletedTask;
        });
    }
}

[KafkaTopic("orders")]
internal sealed class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    [KafkaDecimal(precision: 18, scale: 2)]
    public decimal Amount { get; set; }
}

[KafkaTopic("orders-by-customer")]
internal sealed class OrderByCustomer
{
    public int CustomerId { get; set; }
}

[KafkaTopic("customers")]
internal sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

[KafkaTopic("order-summaries")]
internal sealed class OrderSummary
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
}
