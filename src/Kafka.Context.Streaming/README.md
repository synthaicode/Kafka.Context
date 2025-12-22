# Kafka.Context.Streaming

Engine-agnostic Streaming DSL for Kafka.Context.

Source & docs: https://github.com/synthaicode/Kafka.Context
Streaming API guide: https://github.com/synthaicode/Kafka.Context/blob/main/docs/wiki/streaming-api.md

## Install

```sh
dotnet add package Kafka.Context.Streaming
```

## What this package provides

- Query-building DSL (From/Join/Where/GroupBy/Select/Having).
- Streaming query definitions via `ToQuery(...)`.
- Engine-agnostic query plans consumed by dialect packages (e.g., Flink).

## Minimal usage

```csharp
public sealed class AnalyticsContext : KafkaContext
{
    public AnalyticsContext(IConfiguration configuration) : base(configuration) { }

    public EventSet<Order> Orders { get; set; } = null!;
    public EventSet<OrderStats> OrderStats { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderStats>().ToQuery(q => q
            .From<Order>()
            .GroupBy(o => o.CustomerId)
            .Select(o => new OrderStats
            {
                CustomerId = o.CustomerId,
                Count = FlinkAgg.Count(),
                TotalAmount = FlinkAgg.Sum(o.Amount)
            }));
    }
}
```

## Dialect packages

For actual SQL generation, install a dialect package:

- Flink: `Kafka.Context.Streaming.Flink`

## AI Assist
If you're unsure how to use this package, run `kafka-context ai guide --copy` and paste the output into your AI assistant.
