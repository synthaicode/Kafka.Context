using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Kafka.Context.Configuration;
using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;
using Microsoft.Extensions.Configuration;

namespace FlinkStreamingExamples;

internal static class Program
{
    private static async Task Main()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build();

        Console.WriteLine("== select ==");
        await using (var ctx = new SelectContext(configuration))
            await ctx.ProvisionStreamingAsync();

        Console.WriteLine();
        Console.WriteLine("== where ==");
        await using (var ctx = new WhereContext(configuration))
            await ctx.ProvisionStreamingAsync();

        Console.WriteLine();
        Console.WriteLine("== groupby ==");
        await using (var ctx = new GroupByContext(configuration))
            await ctx.ProvisionStreamingAsync();

        Console.WriteLine();
        Console.WriteLine("== having ==");
        await using (var ctx = new HavingContext(configuration))
            await ctx.ProvisionStreamingAsync();

        Console.WriteLine();
        Console.WriteLine("== join ==");
        await using (var ctx = new JoinContext(configuration))
            await ctx.ProvisionStreamingAsync();

        Console.WriteLine();
        Console.WriteLine("== window tumble ==");
        await using (var ctx = new WindowTumbleContext(configuration))
            await ctx.ProvisionStreamingAsync();

        Console.WriteLine();
        Console.WriteLine("== window hop ==");
        await using (var ctx = new WindowHopContext(configuration))
            await ctx.ProvisionStreamingAsync();
    }
}

internal abstract class FlinkContextBase : KafkaContext
{
    protected FlinkContextBase(IConfiguration configuration)
        : base(configuration)
    {
    }

    protected override IStreamingDialectProvider ResolveStreamingDialectProvider()
    {
        var opts = Configuration.GetKsqlDslOptions();
        var kafka = FlinkKafkaConnectorOptionsFactory.From(opts);

        return new FlinkDialectProvider(kafka, (ddl, ct) =>
        {
            Console.WriteLine(ddl);
            return Task.CompletedTask;
        });
    }
}

// ======================
// select
// ======================

internal sealed class SelectContext : FlinkContextBase
{
    public SelectContext(IConfiguration configuration) : base(configuration) { }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SelectInput>();

        modelBuilder.Entity<SelectOutput>().ToQuery(q => q
            .From<SelectInput>()
            .Select(x => new SelectOutput { Id = x.Id, Name = x.Name }));
    }
}

[KafkaTopic("select-input")]
internal sealed class SelectInput
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

[KafkaTopic("select-output")]
internal sealed class SelectOutput
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

// ======================
// where
// ======================

internal sealed class WhereContext : FlinkContextBase
{
    public WhereContext(IConfiguration configuration) : base(configuration) { }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WhereInput>();

        modelBuilder.Entity<WhereOutput>().ToQuery(q => q
            .From<WhereInput>()
            .Where(x => x.Name.Length > 3 && x.Name.Contains("a_b%"))
            .Select(x => new WhereOutput { Name = x.Name }));
    }
}

[KafkaTopic("where-input")]
internal sealed class WhereInput
{
    public string Name { get; set; } = "";
}

[KafkaTopic("where-output")]
internal sealed class WhereOutput
{
    public string Name { get; set; } = "";
}

// ======================
// groupby
// ======================

internal sealed class GroupByContext : FlinkContextBase
{
    public GroupByContext(IConfiguration configuration) : base(configuration) { }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GroupByInput>();

        modelBuilder.Entity<GroupByOutput>().ToQuery(q => q
            .From<GroupByInput>()
            .GroupBy(x => x.CustomerId)
            .Select(x => new GroupByOutput { CustomerId = x.CustomerId }));
    }
}

[KafkaTopic("groupby-input")]
internal sealed class GroupByInput
{
    public string CustomerId { get; set; } = "";
}

[KafkaTopic("groupby-output")]
internal sealed class GroupByOutput
{
    public string CustomerId { get; set; } = "";
}

// ======================
// having
// ======================

internal sealed class HavingContext : FlinkContextBase
{
    public HavingContext(IConfiguration configuration) : base(configuration) { }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HavingInput>();

        modelBuilder.Entity<HavingOutput>().ToQuery(q => q
            .From<HavingInput>()
            .GroupBy(x => x.CustomerId)
            .Having(x => x.CustomerId != "")
            .Select(x => new HavingOutput { CustomerId = x.CustomerId }));
    }
}

[KafkaTopic("having-input")]
internal sealed class HavingInput
{
    public string CustomerId { get; set; } = "";
}

[KafkaTopic("having-output")]
internal sealed class HavingOutput
{
    public string CustomerId { get; set; } = "";
}

// ======================
// join
// ======================

internal sealed class JoinContext : FlinkContextBase
{
    public JoinContext(IConfiguration configuration) : base(configuration) { }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        modelBuilder.Entity<Customer>();

        // CTAS side (aggregate placeholder)
        modelBuilder.Entity<OrderByCustomer>().ToQuery(q => q
            .From<Order>()
            .GroupBy(o => o.CustomerId)
            .Select(o => new OrderByCustomer { CustomerId = o.CustomerId }));

        // JOIN is allowed only when one side is CTAS-derived.
        modelBuilder.Entity<OrderSummary>().ToQuery(q => q
            .From<OrderByCustomer>()
            .Join<OrderByCustomer, Customer>((x, c) => x.CustomerId == c.Id)
            .Where((x, c) => c.IsActive)
            .Select((x, c) => new OrderSummary { CustomerId = x.CustomerId, CustomerName = c.Name }));
    }
}

[KafkaTopic("orders")]
internal sealed class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
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

[KafkaTopic("order-summary")]
internal sealed class OrderSummary
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
}

// ======================
// window tumble / hop
// ======================

internal sealed class WindowTumbleContext : FlinkContextBase
{
    public WindowTumbleContext(IConfiguration configuration) : base(configuration) { }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WindowInput>()
            .FlinkSource(s => s.EventTimeColumn(x => x.EventTime, watermarkDelay: TimeSpan.FromSeconds(5)));

        modelBuilder.Entity<TumbleAggByCustomer>().ToQuery(q => q
            .From<WindowInput>()
            .TumbleWindow(x => x.EventTime, TimeSpan.FromSeconds(5))
            .GroupBy(x => new { x.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(x => new TumbleAggByCustomer
            {
                CustomerId = x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                Cnt = FlinkAgg.Count(),
                TotalAmount = FlinkAgg.Sum(x.Amount),
            }), outputMode: StreamingOutputMode.Final, sinkMode: StreamingSinkMode.Upsert);
    }
}

internal sealed class WindowHopContext : FlinkContextBase
{
    public WindowHopContext(IConfiguration configuration) : base(configuration) { }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WindowInput>()
            .FlinkSource(s => s.EventTimeColumn(x => x.EventTime, watermarkDelay: TimeSpan.FromSeconds(5)));

        modelBuilder.Entity<HopAggByCustomer>().ToQuery(q => q
            .From<WindowInput>()
            .HopWindow(x => x.EventTime, slide: TimeSpan.FromSeconds(2), size: TimeSpan.FromSeconds(6))
            .GroupBy(x => new { x.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(x => new HopAggByCustomer
            {
                CustomerId = x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                Cnt = FlinkAgg.Count(),
                TotalAmount = FlinkAgg.Sum(x.Amount),
            }), outputMode: StreamingOutputMode.Final, sinkMode: StreamingSinkMode.Upsert);
    }
}

[KafkaTopic("window-input")]
internal sealed class WindowInput
{
    public int CustomerId { get; set; }
    public DateTime EventTime { get; set; }
    public int Amount { get; set; }
}

[KafkaTopic("tumble-agg-by-customer")]
internal sealed class TumbleAggByCustomer
{
    [KafkaKey]
    public int CustomerId { get; set; }
    [KafkaKey]
    public object WindowStart { get; set; } = null!;
    [KafkaKey]
    public object WindowEnd { get; set; } = null!;
    public long Cnt { get; set; }
    public int TotalAmount { get; set; }
}

[KafkaTopic("hop-agg-by-customer")]
internal sealed class HopAggByCustomer
{
    [KafkaKey]
    public int CustomerId { get; set; }
    [KafkaKey]
    public object WindowStart { get; set; } = null!;
    [KafkaKey]
    public object WindowEnd { get; set; } = null!;
    public long Cnt { get; set; }
    public int TotalAmount { get; set; }
}
