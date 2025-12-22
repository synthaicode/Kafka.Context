# AI Development Guide for Kafka.Context

**Target Audience**: AI assistants (GitHub Copilot, Cursor, Claude, ChatGPT, etc.)
**Purpose**: Enable AI to guide developers effectively when working with Kafka.Context
**Last Updated**: 2025-12-22

---

## Quick Reference

### Project Type
- **OSS Library**: Kafka + Schema Registry client with Entity Framework-like API
- **Language**: C# (.NET 8 / .NET 10)
- **Design Philosophy**: Context-centric, minimal API, explicit failure handling
- **Key Namespaces**:
  - `Kafka.Context` - Basic operations (AddAsync/ForEachAsync)
  - `Kafka.Context.Streaming` - Engine-agnostic DSL
  - `Kafka.Context.Streaming.Flink` - Flink SQL implementation

---

## Guide Governance (Kafka.Context)

This guide follows the same operational rules as the Ksql.Linq AI guide rebuild plan.

**Source of truth**:
- Wiki pages, code, examples, and `CHANGELOG.md` are canonical.
- This guide is an AI-oriented summary and must not override the canonical sources.

**Status labels**:
- Every technical statement should be labeled as **Implemented**, **Preview**, or **Planned**.
- If status is unknown, say so explicitly and point to the canonical source.

**Change tracking**:
- Every material update must add a diff log entry under `docs/diff_log/` using
  `diff_ai_development_guide_<section>_<YYYYMMDD>.md`.
- Update the mapping file `docs/ai_development_guide_wiki_mapping.md` with the
  wiki/code sources used for the change.

**No phantom APIs**:
- Do not document APIs that are not in code or `PublicAPI.*.txt`.

---

## 1. Basic Usage Pattern (95% of use cases)

### Pattern: Simple Produce/Consume

```csharp
// Step 1: Define entity with attributes
[KafkaTopic("orders")]
public class Order
{
    public int Id { get; set; }

    [KafkaDecimal(precision: 18, scale: 2)]
    public decimal Amount { get; set; }
}

// Step 2: Define context
public class OrderContext : KafkaContext
{
    public OrderContext(IConfiguration configuration, ILoggerFactory? loggerFactory = null)
        : base(configuration, loggerFactory) { }

    public EventSet<Order> Orders { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
        => modelBuilder.Entity<Order>();
}

// Step 3: Use context
await using var context = new OrderContext(configuration, loggerFactory);

// Produce
await context.Orders.AddAsync(new Order { Id = 1, Amount = 10m });

// Consume
await context.Orders.ForEachAsync((order, headers, meta) => {
    Console.WriteLine($"Order {order.Id}: {order.Amount}");
    return Task.CompletedTask;
});
```

**AI Guidance Points**:
- Always inherit from `KafkaContext`
- Use `EventSet<T>` for topic access (like EF's `DbSet<T>`)
- Call `OnModelCreating` to register entities
- Use `AddAsync` for producing, `ForEachAsync` for consuming

---

## 2. Advanced Pattern: Streaming DSL (5% of use cases)

### When to Use Streaming DSL

**Suggest Streaming DSL when developer needs**:
- Stream JOINs
- Windowed aggregations (tumbling/hopping/session windows)
- Derived outputs (CTAS-like transformations)
- SQL-like stream queries

**Do NOT suggest Streaming DSL for**:
- Simple produce/consume
- Single message transformations
- Stateless filtering

### Pattern: Window Aggregation

```csharp
public class AnalyticsContext : KafkaContext
{
    public AnalyticsContext(IConfiguration configuration) : base(configuration) { }

    public EventSet<Order> Orders { get; set; } = null!;
    public EventSet<OrderStats> OrderStats { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // Source
        modelBuilder.Entity<Order>()
            .FlinkSource(s => s.EventTimeColumn(
                x => x.OrderTime,
                watermarkDelay: TimeSpan.FromSeconds(5)));

        // Derived output (window aggregation)
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
            Console.WriteLine(ddl); // Or execute via Flink SQL Gateway
            return Task.CompletedTask;
        });
    }
}

// Provision streaming resources
await context.ProvisionStreamingAsync();
```

**AI Guidance Points**:
- Use `.FlinkSource()` to configure event-time for windowing
- Use `FlinkWindow.Start()/End()` for window bounds
- Use `FlinkAgg.*` for aggregations (Count/Sum/Avg)
- Always specify `WindowStart` and `WindowEnd` in GroupBy for window queries
- Set `outputMode: Final` and `sinkMode: Upsert` for window aggregations

---

## 3. Critical Constraints (MUST ENFORCE)

### Streaming DSL Constraints

| Constraint | Rule | AI Response When Violated |
|-----------|------|--------------------------|
| **Window + JOIN** | Window queries CANNOT be combined with JOIN in a single query | "Split into 2 queries: 1) Window aggregation (CTAS), 2) JOIN with the result" |
| **Window GroupBy** | Window queries MUST include `WindowStart` and `WindowEnd` in GroupBy | "Add `WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End()` to GroupBy" |
| **OutputMode.Final** | Only allowed for window queries | "Remove `outputMode: Final` or add a window (TumbleWindow/HopWindow)" |
| **SinkMode.Upsert** | Only allowed for window + CTAS queries | "Use with window aggregations, or change to `StreamingSinkMode.AppendOnly`" |
| **EventTime Required** | Window queries require `.FlinkSource(s => s.EventTimeColumn(...))` | "Configure event-time: `b.Entity<T>().FlinkSource(s => s.EventTimeColumn(x => x.Timestamp, ...))`" |

### Example: Fixing Window + JOIN Violation

**WRONG**:
```csharp
b.Entity<Result>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5)) // Window
    .Join<Order, Customer>((o, c) => o.CustomerId == c.Id)   // JOIN - ERROR!
    .GroupBy(...)
    .Select(...));
```

**CORRECT**:
```csharp
// Step 1: Create windowed aggregation
b.Entity<WindowedOrders>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
    .GroupBy(x => new {
        x.CustomerId,
        WindowStart = FlinkWindow.Start(),
        WindowEnd = FlinkWindow.End()
    })
    .Select(...));

// Step 2: JOIN with windowed result
b.Entity<Result>().ToQuery(q => q
    .From<WindowedOrders>()
    .Join<WindowedOrders, Customer>((w, c) => w.CustomerId == c.Id)
    .Select(...));
```

---

## 4. Common Attributes

### Entity Attributes

```csharp
// Topic binding (REQUIRED)
[KafkaTopic("order-events")]
public class Order { }

// Key fields (for upsert/compaction)
public class Order
{
    [KafkaKey]
    public int OrderId { get; set; }
}

// Decimal precision (Avro logical type)
public class Order
{
    [KafkaDecimal(precision: 18, scale: 2)]
    public decimal Amount { get; set; }
}

// Timestamp (event-time marker)
public class Order
{
    [KafkaTimestamp]
    public DateTime OrderTime { get; set; }
}

// Schema Registry subject (explicit)
[SchemaSubject("custom-order-value")]
public class Order { }
```

**AI Guidance**:
- Always add `[KafkaTopic]` to entity classes
- Use `[KafkaKey]` for primary keys in upsert scenarios
- Use `[KafkaDecimal]` for decimal properties (Avro requires precision/scale)
- Use `[KafkaTimestamp]` to mark event-time columns

---

## 5. Error Handling Patterns

### Pattern: Retry + DLQ

```csharp
await context.Orders
    .OnError(ErrorAction.DLQ)  // Send to DLQ on error
    .WithRetry(3)               // Retry up to 3 times
    .ForEachAsync(async (order, headers, meta) => {
        if (order.Amount < 0)
            throw new InvalidOperationException("Negative amount");

        await ProcessOrder(order);
    });
```

### Pattern: Manual Commit

```csharp
await context.Orders.ForEachAsync(async (order, headers, meta) => {
    await ProcessOrder(order);
    context.Orders.Commit(meta);  // Manual commit after processing
}, autoCommit: false);
```

**AI Guidance**:
- Suggest `.OnError(ErrorAction.DLQ).WithRetry(N)` for production workloads
- Use manual commit for exactly-once semantics
- Default is auto-commit (at-least-once)

---

## 6. Flink-Specific Functions

### Window Functions

```csharp
FlinkWindow.Start()      // window_start (TIMESTAMP(3))
FlinkWindow.End()        // window_end (TIMESTAMP(3))
FlinkWindow.Proctime()   // PROCTIME() (processing-time)
```

### Aggregate Functions

```csharp
FlinkAgg.Count()         // COUNT(*)
FlinkAgg.Sum(x.Amount)   // SUM(amount)
FlinkAgg.Avg(x.Amount)   // AVG(amount)
```

### String Functions

```csharp
FlinkSql.Concat(str1, str2)              // CONCAT(str1, str2)
FlinkSql.RegexpExtract(input, pattern, group)  // REGEXP_EXTRACT(...)
string.Contains("pattern")                // LIKE '%pattern%'
string.ToUpper()                         // UPPER(...)
```

### Date/Time Functions

```csharp
FlinkSql.CurrentTimestamp()              // CURRENT_TIMESTAMP
FlinkSql.DateFormat(timestamp, format)   // DATE_FORMAT(...)
FlinkSql.TimestampDiff(unit, start, end) // TIMESTAMPDIFF(...)
DateTime.Year / Month / Day / Hour       // EXTRACT(YEAR FROM ...)
```

### Array/Map Functions

```csharp
FlinkSql.Array(1, 2, 3)                 // ARRAY[1, 2, 3]
FlinkSql.ArrayLength(array)             // CARDINALITY(array)
FlinkSql.Map("k1", "v1", "k2", "v2")    // MAP['k1', 'v1', 'k2', 'v2']
```

**AI Guidance**:
- Use `FlinkSql.*` for Flink-specific functions
- Use `FlinkWindow.*` for window bounds
- Use `FlinkAgg.*` for aggregations
- Many standard C# methods are mapped (e.g., `string.Contains` -> `LIKE`)

---

## 7. Configuration (appsettings.json)

### Minimal Configuration

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "SchemaRegistry": {
    "Url": "http://localhost:8081"
  }
}
```

### Streaming DSL Configuration (Flink)

```json
{
  "KsqlDsl": {
    "Common": {
      "BootstrapServers": "localhost:9092"
    },
    "SchemaRegistry": {
      "Url": "http://localhost:8081"
    },
    "Streaming": {
      "Flink": {
        "ScanStartupMode": "earliest-offset",
        "SourceGroupIdByTopic": {
          "orders": "order-consumer-group"
        },
        "Sources": {
          "orders": {
            "scan.startup.mode": "group-offsets"
          }
        },
        "Sinks": {
          "order-stats": {
            "sink.parallelism": "4"
          }
        }
      }
    }
  }
}
```

**AI Guidance**:
- Basic usage requires `Kafka:BootstrapServers` and `SchemaRegistry:Url`
- Streaming DSL uses `KsqlDsl:Streaming:Flink` section
- Per-topic settings go in `Sources:<topic>` or `Sinks:<topic>`

---

## 8. Troubleshooting Guide for AI

### Issue: "Window TVF cannot be combined with JOIN"

**User Intent**: Create a windowed aggregation with JOIN

**AI Response**:
```
This error occurs because Flink doesn't support window + JOIN in a single query.

Solution: Split into 2 queries:

1. Create windowed aggregation (CTAS):
[Code example]

2. JOIN the windowed result:
[Code example]
```

### Issue: "OutputMode.Final is only supported for window queries"

**User Intent**: Use Final output mode for non-window query

**AI Response**:
```
OutputMode.Final requires a window query (TUMBLE/HOP/SESSION).

Options:
1. Add a window: .TumbleWindow(x => x.Timestamp, TimeSpan.FromMinutes(5))
2. Change to: outputMode: StreamingOutputMode.Changelog (default)
```

### Issue: Window not firing

**User Intent**: Window aggregation produces no output

**AI Response**:
```
Windows require event-time configuration and watermarks.

Checklist:
1. Configure event-time column:
   b.Entity<Order>().FlinkSource(s =>
       s.EventTimeColumn(x => x.OrderTime, watermarkDelay: TimeSpan.FromSeconds(5)));

2. Ensure event-time values are monotonically increasing

3. Check watermarkDelay (increase if events are out-of-order)
```

---

## 9. Code Generation Templates

### Template: Basic Context

**When to use**: User says "create a Kafka context for [Entity]"

```csharp
[KafkaTopic("{topic-name}")]
public class {EntityName}
{
    public int Id { get; set; }
    // Add other properties...
}

public class {EntityName}Context : KafkaContext
{
    public {EntityName}Context(IConfiguration configuration, ILoggerFactory? loggerFactory = null)
        : base(configuration, loggerFactory) { }

    public EventSet<{EntityName}> {PluralEntityName} { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
        => modelBuilder.Entity<{EntityName}>();
}
```

### Template: Streaming Context with Window

**When to use**: User says "aggregate [Entity] by time window"

```csharp
public class {EntityName}AnalyticsContext : KafkaContext
{
    public {EntityName}AnalyticsContext(IConfiguration configuration) : base(configuration) { }

    public EventSet<{InputEntity}> {InputEntityPlural} { get; set; } = null!;
    public EventSet<{OutputEntity}> {OutputEntityPlural} { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // Configure event-time
        modelBuilder.Entity<{InputEntity}>()
            .FlinkSource(s => s.EventTimeColumn(
                x => x.{TimestampProperty},
                watermarkDelay: TimeSpan.FromSeconds(5)));

        // Window aggregation
        modelBuilder.Entity<{OutputEntity}>().ToQuery(q => q
            .From<{InputEntity}>()
            .TumbleWindow(x => x.{TimestampProperty}, TimeSpan.FromMinutes({WindowSize}))
            .GroupBy(x => new {
                x.{GroupByKey},
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End()
            })
            .Select(x => new {OutputEntity} {
                {GroupByKey} = x.{GroupByKey},
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                Count = FlinkAgg.Count(),
                // Add aggregations...
            }),
            outputMode: StreamingOutputMode.Final,
            sinkMode: StreamingSinkMode.Upsert);
    }

    protected override IStreamingDialectProvider ResolveStreamingDialectProvider()
    {
        var opts = Configuration.GetKsqlDslOptions();
        var kafka = FlinkKafkaConnectorOptionsFactory.From(opts);
        return new FlinkDialectProvider(kafka, ExecuteDdl);
    }

    private Task ExecuteDdl(string ddl, CancellationToken ct)
    {
        Console.WriteLine(ddl);
        // TODO: Execute via Flink SQL Gateway
        return Task.CompletedTask;
    }
}
```

---

## 10. Conversation Rules

Conversation guidance has been split into a dedicated file:

- `docs/ai_guide_conversation_rules.md`

Use that file for decision trees, best practices, and validation checklists.

---
## 13. Version Compatibility

- Target frameworks: .NET 8, .NET 10
- C# language version: 12+
- Kafka: Compatible with Kafka 2.x, 3.x
- Schema Registry: Confluent Schema Registry 5.x+
- Flink: Apache Flink 1.18+ (for Streaming DSL)
- Avro format: `avro-confluent` only (Schema Registry integration)

---

## 14. Related Files

- Architecture Review: `ARCHITECTURE_REVIEW_STREAMING_DSL.md`
- Learning Cost Analysis: `LEARNING_COST_ANALYSIS.md`
- Examples: `examples/quickstart/`, `examples/streaming-flink/`
- API Documentation: `docs/wiki/streaming-api.md`
- GitHub Copilot Instructions: `.github/copilot-instructions.md`

---

## 15. AI Prompt Examples

### Example 1: User asks for basic Kafka producer

**User**: "Create a Kafka producer for Order events"

**AI Should Respond**:
```csharp
// 1. Define entity
[KafkaTopic("orders")]
public class Order
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = "";

    [KafkaDecimal(precision: 18, scale: 2)]
    public decimal Amount { get; set; }
}

// 2. Define context
public class OrderContext : KafkaContext
{
    public OrderContext(IConfiguration configuration, ILoggerFactory? loggerFactory = null)
        : base(configuration, loggerFactory) { }

    public EventSet<Order> Orders { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
        => modelBuilder.Entity<Order>();
}

// 3. Produce events
await using var context = new OrderContext(configuration, loggerFactory);
await context.Orders.AddAsync(new Order {
    OrderId = 1,
    CustomerId = "CUST123",
    Amount = 99.99m
});
```

### Example 2: User asks for windowed aggregation

**User**: "Aggregate orders by customer every 5 minutes"

**AI Should Respond**:
```csharp
// 1. Ensure Order has timestamp
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

// 2. Define output entity
[KafkaTopic("order-stats-5min")]
public class OrderStats
{
    [KafkaKey]
    public string CustomerId { get; set; } = "";

    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public long OrderCount { get; set; }

    [KafkaDecimal(precision: 18, scale: 2)]
    public decimal TotalAmount { get; set; }
}

// 3. Define context with streaming query
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

        // 5-minute tumbling window aggregation
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
                OrderCount = FlinkAgg.Count(),
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

// 4. Provision streaming resources
await using var context = new OrderAnalyticsContext(configuration);
await context.ProvisionStreamingAsync();
```

---

**End of AI Development Guide**

This guide is optimized for AI parsing. All patterns, constraints, and examples are structured for quick lookup and code generation by AI assistants.

