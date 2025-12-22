# Kafka.Context.Streaming.Flink

Flink SQL dialect implementation for the Kafka.Context Streaming DSL.

Source & docs: https://github.com/synthaicode/Kafka.Context
Streaming API guide: https://github.com/synthaicode/Kafka.Context/blob/main/docs/wiki/streaming-api.md

## Install

```sh
dotnet add package Kafka.Context.Streaming.Flink
```

## What this package provides

- Flink SQL rendering for the engine-agnostic Streaming DSL.
- Flink-specific functions: `FlinkSql.*`, `FlinkAgg.*`, `FlinkWindow.*`.
- DDL generation via `ProvisionStreamingAsync()` when a Flink dialect provider is configured.

## Minimal usage

```csharp
public sealed class AnalyticsContext : KafkaContext
{
    public AnalyticsContext(IConfiguration configuration) : base(configuration) { }

    public EventSet<Order> Orders { get; set; } = null!;
    public EventSet<OrderStats> OrderStats { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .FlinkSource(s => s.EventTimeColumn(x => x.OrderTime, watermarkDelay: TimeSpan.FromSeconds(5)));

        modelBuilder.Entity<OrderStats>().ToQuery(q => q
            .From<Order>()
            .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
            .GroupBy(x => new {
                x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End()
            })
            .Select(x => new OrderStats {
                CustomerId = x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                TotalOrders = FlinkAgg.Count(),
                TotalAmount = FlinkAgg.Sum(x.Amount)
            }),
            outputMode: StreamingOutputMode.Final,
            sinkMode: StreamingSinkMode.Upsert);
    }

    protected override IStreamingDialectProvider ResolveStreamingDialectProvider()
    {
        var opts = Configuration.GetKsqlDslOptions();
        var kafka = FlinkKafkaConnectorOptionsFactory.From(opts);
        return new FlinkDialectProvider(kafka, (ddl, ct) => Task.CompletedTask);
    }
}
```

## Constraints (summary)

- Window queries cannot be combined with JOIN in a single query.
- Window GroupBy must include `FlinkWindow.Start()` and `FlinkWindow.End()`.
- `StreamingOutputMode.Final` is only allowed for window queries.
- `StreamingSinkMode.Upsert` is only allowed for window + CTAS queries.

## Examples

- https://github.com/synthaicode/Kafka.Context/tree/main/examples/streaming-flink
- https://github.com/synthaicode/Kafka.Context/tree/main/examples/streaming-flink-flow

## AI Assist
If you're unsure how to use this package, run `kafka-context ai guide --copy` and paste the output into your AI assistant.
