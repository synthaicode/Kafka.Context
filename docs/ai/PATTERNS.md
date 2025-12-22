# AI Code Pattern Library

Quick reference for AI assistants generating Kafka.Context code.

## Pattern Index

1. [Basic Producer](#pattern-1-basic-producer)
2. [Basic Consumer](#pattern-2-basic-consumer)
3. [Producer + Consumer](#pattern-3-producer--consumer)
4. [Error Handling (DLQ)](#pattern-4-error-handling-dlq)
5. [Manual Commit](#pattern-5-manual-commit)
6. [Window Aggregation](#pattern-6-window-aggregation)
7. [Stream JOIN](#pattern-7-stream-join)
8. [Window + JOIN (Split)](#pattern-8-window--join-split-pattern)

---

## Pattern 1: Basic Producer

**Use Case**: Produce messages to Kafka topic

**Code**:
```csharp
using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

[KafkaTopic("events")]
public class Event
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
}

public class EventContext : KafkaContext
{
    public EventContext(IConfiguration configuration, ILoggerFactory? loggerFactory = null)
        : base(configuration, loggerFactory) { }

    public EventSet<Event> Events { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
        => modelBuilder.Entity<Event>();
}

// Usage
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

await using var context = new EventContext(configuration);
await context.Events.AddAsync(new Event { Id = 1, Message = "Hello" });
```

---

## Pattern 2: Basic Consumer

**Use Case**: Consume messages from Kafka topic

**Code**:
```csharp
await using var context = new EventContext(configuration, loggerFactory);

await context.Events.ForEachAsync((evt, headers, meta) =>
{
    Console.WriteLine($"Event {evt.Id}: {evt.Message}");
    Console.WriteLine($"Partition: {meta.Partition}, Offset: {meta.Offset}");
    return Task.CompletedTask;
});
```

---

## Pattern 3: Producer + Consumer

**Use Case**: Full produce/consume cycle

**Code**:
```csharp
await using var context = new EventContext(configuration, loggerFactory);

// Produce
await context.Events.AddAsync(new Event { Id = 1, Message = "Test" });
await Task.Delay(500); // Wait for message to be available

// Consume
await context.Events.ForEachAsync(
    (evt, headers, meta) => {
        Console.WriteLine($"Received: {evt.Id}");
        return Task.CompletedTask;
    },
    timeout: TimeSpan.FromSeconds(5));
```

---

## Pattern 4: Error Handling (DLQ)

**Use Case**: Production workload with retry + DLQ

**Code**:
```csharp
await context.Events
    .OnError(ErrorAction.DLQ)  // Send to DLQ on error
    .WithRetry(3)               // Retry up to 3 times
    .ForEachAsync(async (evt, headers, meta) =>
    {
        if (evt.Id < 0)
            throw new InvalidOperationException("Invalid ID");

        await ProcessEvent(evt);
    });
```

**DLQ Topic**: Automatically created as `{topic-name}-dlq`

---

## Pattern 5: Manual Commit

**Use Case**: Exactly-once semantics

**Code**:
```csharp
await context.Events.ForEachAsync(async (evt, headers, meta) =>
{
    await ProcessEvent(evt);
    await SaveToDatabase(evt);

    // Commit only after successful processing
    context.Events.Commit(meta);
}, autoCommit: false);
```

---

## Pattern 6: Window Aggregation

**Use Case**: Time-based aggregations (e.g., "orders per 5-minute window")

**Code**:
```csharp
using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;

[KafkaTopic("orders")]
public class Order
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = "";

    [KafkaDecimal(precision: 18, scale: 2)]
    public decimal Amount { get; set; }

    [KafkaTimestamp]
    public DateTime OrderTime { get; set; }
}

[KafkaTopic("order-stats")]
public class OrderStats
{
    [KafkaKey]
    public string CustomerId { get; set; } = "";

    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public long TotalOrders { get; set; }

    [KafkaDecimal(precision: 18, scale: 2)]
    public decimal TotalAmount { get; set; }
}

public class OrderAnalyticsContext : KafkaContext
{
    public OrderAnalyticsContext(IConfiguration configuration) : base(configuration) { }

    public EventSet<Order> Orders { get; set; } = null!;
    public EventSet<OrderStats> OrderStats { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // Configure event-time
        modelBuilder.Entity<Order>().FlinkSource(s =>
            s.EventTimeColumn(x => x.OrderTime, watermarkDelay: TimeSpan.FromSeconds(5)));

        // 5-minute tumbling window
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
        return new FlinkDialectProvider(kafka, (ddl, ct) => {
            Console.WriteLine(ddl);
            return Task.CompletedTask;
        });
    }
}

// Provision streaming resources
await using var context = new OrderAnalyticsContext(configuration);
await context.ProvisionStreamingAsync();
```

**Key Points**:
- Use `.FlinkSource()` to configure event-time
- Include `WindowStart`/`WindowEnd` in GroupBy
- Set `outputMode: Final` and `sinkMode: Upsert`

---

## Pattern 7: Stream JOIN

**Use Case**: Enrich stream with data from another stream

**Code**:
```csharp
[KafkaTopic("orders")]
public class Order
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
}

[KafkaTopic("customers")]
public class Customer
{
    public string CustomerId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

[KafkaTopic("enriched-orders")]
public class EnrichedOrder
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal Amount { get; set; }
}

public class OrderEnrichmentContext : KafkaContext
{
    public OrderEnrichmentContext(IConfiguration configuration) : base(configuration) { }

    public EventSet<Order> Orders { get; set; } = null!;
    public EventSet<Customer> Customers { get; set; } = null!;
    public EventSet<EnrichedOrder> EnrichedOrders { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        modelBuilder.Entity<Customer>();

        // JOIN orders with customers
        modelBuilder.Entity<EnrichedOrder>().ToQuery(q => q
            .From<Order>()
            .Join<Order, Customer>((o, c) => o.CustomerId == c.CustomerId)
            .Where((o, c) => c.IsActive)
            .Select((o, c) => new EnrichedOrder {
                OrderId = o.OrderId,
                CustomerId = o.CustomerId,
                CustomerName = c.Name,
                Amount = o.Amount
            }));
    }

    protected override IStreamingDialectProvider ResolveStreamingDialectProvider()
    {
        var opts = Configuration.GetKsqlDslOptions();
        var kafka = FlinkKafkaConnectorOptionsFactory.From(opts);
        return new FlinkDialectProvider(kafka, (ddl, ct) => {
            Console.WriteLine(ddl);
            return Task.CompletedTask;
        });
    }
}
```

---

## Pattern 8: Window + JOIN (Split Pattern)

**Use Case**: JOIN windowed aggregation with another stream

**⚠️ Constraint**: Window + JOIN cannot be in the same query. Must split into 2 queries.

**Code**:
```csharp
[KafkaTopic("orders")]
public class Order
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
    [KafkaTimestamp]
    public DateTime OrderTime { get; set; }
}

[KafkaTopic("customers")]
public class Customer
{
    public string CustomerId { get; set; } = "";
    public string Name { get; set; } = "";
}

[KafkaTopic("windowed-orders")]
public class WindowedOrders
{
    [KafkaKey]
    public string CustomerId { get; set; } = "";
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public long OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
}

[KafkaTopic("enriched-windowed-orders")]
public class EnrichedWindowedOrders
{
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public long OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ComplexAnalyticsContext : KafkaContext
{
    public ComplexAnalyticsContext(IConfiguration configuration) : base(configuration) { }

    public EventSet<Order> Orders { get; set; } = null!;
    public EventSet<Customer> Customers { get; set; } = null!;
    public EventSet<WindowedOrders> WindowedOrders { get; set; } = null!;
    public EventSet<EnrichedWindowedOrders> EnrichedWindowedOrders { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>();

        // Configure event-time
        modelBuilder.Entity<Order>().FlinkSource(s =>
            s.EventTimeColumn(x => x.OrderTime, watermarkDelay: TimeSpan.FromSeconds(5)));

        // STEP 1: Window aggregation (CTAS)
        modelBuilder.Entity<WindowedOrders>().ToQuery(q => q
            .From<Order>()
            .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
            .GroupBy(x => new {
                x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End()
            })
            .Select(x => new WindowedOrders {
                CustomerId = x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                OrderCount = FlinkAgg.Count(),
                TotalAmount = FlinkAgg.Sum(x.Amount)
            }),
            outputMode: StreamingOutputMode.Final,
            sinkMode: StreamingSinkMode.Upsert);

        // STEP 2: JOIN windowed result with customers
        modelBuilder.Entity<EnrichedWindowedOrders>().ToQuery(q => q
            .From<WindowedOrders>()
            .Join<WindowedOrders, Customer>((w, c) => w.CustomerId == c.CustomerId)
            .Select((w, c) => new EnrichedWindowedOrders {
                CustomerId = w.CustomerId,
                CustomerName = c.Name,
                WindowStart = w.WindowStart,
                WindowEnd = w.WindowEnd,
                OrderCount = w.OrderCount,
                TotalAmount = w.TotalAmount
            }));
    }

    protected override IStreamingDialectProvider ResolveStreamingDialectProvider()
    {
        var opts = Configuration.GetKsqlDslOptions();
        var kafka = FlinkKafkaConnectorOptionsFactory.From(opts);
        return new FlinkDialectProvider(kafka, (ddl, ct) => {
            Console.WriteLine(ddl);
            return Task.CompletedTask;
        });
    }
}
```

**Why Split?**
- Flink SQL doesn't support window TVF + JOIN in a single query
- Solution: Create intermediate CTAS table, then JOIN with it

---

## Anti-Patterns (DON'T DO THIS)

### ❌ Anti-Pattern 1: Window + JOIN in Same Query

```csharp
// THIS WILL FAIL!
modelBuilder.Entity<Result>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))  // Window
    .Join<Order, Customer>((o, c) => o.CustomerId == c.Id)    // JOIN - ERROR!
    .GroupBy(...)
    .Select(...));

// Error: "Window TVF cannot be combined with JOIN in a single query"
```

**Fix**: Use Pattern 8 (split into 2 queries)

### ❌ Anti-Pattern 2: Missing WindowStart/WindowEnd in GroupBy

```csharp
// THIS WILL FAIL!
modelBuilder.Entity<Stats>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
    .GroupBy(x => x.CustomerId)  // Missing WindowStart/WindowEnd!
    .Select(...));
```

**Fix**:
```csharp
.GroupBy(x => new {
    x.CustomerId,
    WindowStart = FlinkWindow.Start(),
    WindowEnd = FlinkWindow.End()
})
```

### ❌ Anti-Pattern 3: OutputMode.Final without Window

```csharp
// THIS WILL FAIL!
modelBuilder.Entity<Result>().ToQuery(q => q
    .From<Order>()
    .GroupBy(x => x.CustomerId)  // No window!
    .Select(...),
    outputMode: StreamingOutputMode.Final);  // ERROR!
```

**Fix**: Add window or use `StreamingOutputMode.Changelog`

### ❌ Anti-Pattern 4: Forgetting EventTimeColumn for Windows

```csharp
// THIS WILL FAIL!
modelBuilder.Entity<Stats>().ToQuery(q => q
    .From<Order>()  // No EventTimeColumn configured!
    .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
    .GroupBy(...)
    .Select(...));
```

**Fix**:
```csharp
modelBuilder.Entity<Order>().FlinkSource(s =>
    s.EventTimeColumn(x => x.OrderTime, watermarkDelay: TimeSpan.FromSeconds(5)));
```

---

## Pattern Selection Decision Tree

```
What does the user need?

Simple produce?
└─ Use Pattern 1: Basic Producer

Simple consume?
└─ Use Pattern 2: Basic Consumer

Produce + consume?
└─ Use Pattern 3: Producer + Consumer

Production workload with error handling?
└─ Use Pattern 4: Error Handling (DLQ)

Exactly-once semantics?
└─ Use Pattern 5: Manual Commit

Time-based aggregations?
└─ Use Pattern 6: Window Aggregation

Stream enrichment (no window)?
└─ Use Pattern 7: Stream JOIN

Window aggregation + enrichment?
└─ Use Pattern 8: Window + JOIN (Split)
```

---

## Testing Patterns

### Pattern: Test with In-Memory Kafka

```csharp
// Use Docker Compose for local testing
// See: examples/streaming-flink-flow/README.md
```

### Pattern: DDL Preview (No Execution)

```csharp
protected override IStreamingDialectProvider ResolveStreamingDialectProvider()
{
    var opts = Configuration.GetKsqlDslOptions();
    var kafka = FlinkKafkaConnectorOptionsFactory.From(opts);
    return new FlinkDialectProvider(kafka, (ddl, ct) => {
        Console.WriteLine("=== Generated DDL ===");
        Console.WriteLine(ddl);
        Console.WriteLine("(Not executed - preview only)");
        return Task.CompletedTask;
    });
}
```

---

**End of Pattern Library**

For complete context and constraints, see `AI_DEVELOPMENT_GUIDE.md`.
