# Streaming DSL Constraints and Rules

**Target Audience**: AI assistants
**Purpose**: Hard constraints that MUST be enforced when generating code

---

## Critical Constraints (NEVER VIOLATE)

### Constraint 1: Window + JOIN Split

**Rule**: Window queries CANNOT be combined with JOIN in a single query

**Detection**:
```csharp
// Look for this pattern:
.TumbleWindow(...) or .HopWindow(...) or .SessionWindow(...)
  AND
.Join<T1, T2>(...)
```

**Error Message**:
```
Window TVF cannot be combined with JOIN in a single query. Use CTAS first, then JOIN.
```

**AI Action**:
```
1. Detect window + JOIN in same ToQuery
2. Suggest split into 2 queries:
   - Query 1: Window aggregation (CTAS)
   - Query 2: JOIN with windowed result
3. Provide code example (see Pattern 8 in PATTERNS.md)
```

**Example Fix**:
```csharp
// WRONG ❌
b.Entity<Result>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.Time, TimeSpan.FromMinutes(5))
    .Join<Order, Customer>((o, c) => o.Id == c.Id)  // ERROR!
    .GroupBy(...)
    .Select(...));

// CORRECT ✅
// Step 1: Window aggregation
b.Entity<WindowedOrders>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.Time, TimeSpan.FromMinutes(5))
    .GroupBy(x => new {
        x.CustomerId,
        WindowStart = FlinkWindow.Start(),
        WindowEnd = FlinkWindow.End()
    })
    .Select(...));

// Step 2: JOIN
b.Entity<Result>().ToQuery(q => q
    .From<WindowedOrders>()
    .Join<WindowedOrders, Customer>((w, c) => w.CustomerId == c.Id)
    .Select(...));
```

---

### Constraint 2: Window GroupBy Must Include WindowStart/WindowEnd

**Rule**: Window queries MUST include `WindowStart = FlinkWindow.Start()` and `WindowEnd = FlinkWindow.End()` in GroupBy

**Detection**:
```csharp
// Look for:
.TumbleWindow(...) or .HopWindow(...) or .SessionWindow(...)
  AND
.GroupBy(x => new { ... })
  WHERE
  GroupBy does NOT contain FlinkWindow.Start() AND FlinkWindow.End()
```

**AI Action**:
```
1. Detect window query with GroupBy
2. Check if GroupBy includes both WindowStart and WindowEnd
3. If missing, suggest adding them
```

**Example Fix**:
```csharp
// WRONG ❌
.GroupBy(x => x.CustomerId)  // Missing window bounds!

// CORRECT ✅
.GroupBy(x => new {
    x.CustomerId,
    WindowStart = FlinkWindow.Start(),
    WindowEnd = FlinkWindow.End()
})
```

---

### Constraint 3: OutputMode.Final Requires Window

**Rule**: `outputMode: StreamingOutputMode.Final` is ONLY allowed for window queries

**Detection**:
```csharp
// Look for:
.ToQuery(..., outputMode: StreamingOutputMode.Final)
  WHERE
  Query does NOT have .TumbleWindow/HopWindow/SessionWindow
```

**Error Message**:
```
OutputMode.Final is only supported for window queries (TUMBLE/HOP) in Flink dialect.
```

**AI Action**:
```
1. Detect OutputMode.Final without window
2. Suggest either:
   - Add a window: .TumbleWindow(...)
   - Change to: outputMode: StreamingOutputMode.Changelog
```

**Example Fix**:
```csharp
// WRONG ❌
b.Entity<Stats>().ToQuery(q => q
    .From<Order>()
    .GroupBy(x => x.CustomerId)  // No window!
    .Select(...),
    outputMode: StreamingOutputMode.Final);  // ERROR!

// CORRECT ✅ (Option 1: Add window)
b.Entity<Stats>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
    .GroupBy(x => new {
        x.CustomerId,
        WindowStart = FlinkWindow.Start(),
        WindowEnd = FlinkWindow.End()
    })
    .Select(...),
    outputMode: StreamingOutputMode.Final);

// CORRECT ✅ (Option 2: Remove Final)
b.Entity<Stats>().ToQuery(q => q
    .From<Order>()
    .GroupBy(x => x.CustomerId)
    .Select(...),
    outputMode: StreamingOutputMode.Changelog);
```

---

### Constraint 4: SinkMode.Upsert Requires Window + CTAS

**Rule**: `sinkMode: StreamingSinkMode.Upsert` is ONLY allowed for window + CTAS queries

**Detection**:
```csharp
// Look for:
.ToQuery(..., sinkMode: StreamingSinkMode.Upsert)
  WHERE
  Query does NOT have window + GroupBy
```

**AI Action**:
```
1. Detect SinkMode.Upsert without window
2. Suggest either:
   - Add window + aggregation
   - Change to: sinkMode: StreamingSinkMode.AppendOnly
```

**Example Fix**:
```csharp
// WRONG ❌
b.Entity<Result>().ToQuery(q => q
    .From<Order>()
    .Select(...),
    sinkMode: StreamingSinkMode.Upsert);  // ERROR! No window

// CORRECT ✅
b.Entity<Result>().ToQuery(q => q
    .From<Order>()
    .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
    .GroupBy(x => new {
        x.CustomerId,
        WindowStart = FlinkWindow.Start(),
        WindowEnd = FlinkWindow.End()
    })
    .Select(...),
    outputMode: StreamingOutputMode.Final,
    sinkMode: StreamingSinkMode.Upsert);
```

---

### Constraint 5: Window Requires EventTimeColumn

**Rule**: Window queries REQUIRE `.FlinkSource()` with `EventTimeColumn` configuration

**Detection**:
```csharp
// Look for:
.TumbleWindow(...) or .HopWindow(...) or .SessionWindow(...)
  WHERE
  Source entity does NOT have .FlinkSource(s => s.EventTimeColumn(...))
```

**AI Action**:
```
1. Detect window query
2. Check if source entity has .FlinkSource() with EventTimeColumn
3. If missing, suggest adding it
```

**Example Fix**:
```csharp
// WRONG ❌
protected override void OnModelCreating(IModelBuilder b)
{
    b.Entity<Order>();  // No EventTimeColumn!

    b.Entity<Stats>().ToQuery(q => q
        .From<Order>()
        .TumbleWindow(x => x.OrderTime, ...)  // ERROR! No event-time config
        .GroupBy(...)
        .Select(...));
}

// CORRECT ✅
protected override void OnModelCreating(IModelBuilder b)
{
    b.Entity<Order>().FlinkSource(s =>
        s.EventTimeColumn(x => x.OrderTime, watermarkDelay: TimeSpan.FromSeconds(5)));

    b.Entity<Stats>().ToQuery(q => q
        .From<Order>()
        .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
        .GroupBy(x => new {
            WindowStart = FlinkWindow.Start(),
            WindowEnd = FlinkWindow.End()
        })
        .Select(...));
}
```

---

## Mandatory Attributes

### Attribute 1: [KafkaTopic] (REQUIRED)

**Rule**: ALL entity classes MUST have `[KafkaTopic]` attribute

**AI Action**:
```
1. When generating entity class
2. Always add [KafkaTopic("topic-name")]
3. Suggest kebab-case for topic names
```

**Example**:
```csharp
// WRONG ❌
public class Order { }

// CORRECT ✅
[KafkaTopic("orders")]
public class Order { }
```

---

### Attribute 2: [KafkaDecimal] for Decimal Properties

**Rule**: Decimal properties MUST have `[KafkaDecimal(precision, scale)]` for Avro

**AI Action**:
```
1. When generating decimal property
2. Always add [KafkaDecimal(precision: 18, scale: 2)]
3. Adjust precision/scale if user specifies
```

**Example**:
```csharp
// WRONG ❌
public decimal Amount { get; set; }

// CORRECT ✅
[KafkaDecimal(precision: 18, scale: 2)]
public decimal Amount { get; set; }
```

---

### Attribute 3: [KafkaKey] for Upsert Primary Keys

**Rule**: Upsert sinks MUST have `[KafkaKey]` on primary key properties

**AI Action**:
```
1. When sinkMode: Upsert is used
2. Ensure output entity has [KafkaKey] on PK properties
3. If missing, suggest adding it
```

**Example**:
```csharp
[KafkaTopic("order-stats")]
public class OrderStats
{
    [KafkaKey]
    public string CustomerId { get; set; } = "";

    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    // ... other properties
}
```

---

## JOIN Constraints

### JOIN Constraint 1: INNER Only

**Rule**: Only INNER JOIN is supported

**AI Action**:
```
If user asks for LEFT/RIGHT/OUTER join:
"Currently only INNER JOIN is supported. Use .Join<T1, T2>(...)"
```

---

### JOIN Constraint 2: Key Equality Only

**Rule**: JOIN predicates MUST be key equality (conjunctions of `==` between member accesses)

**Example**:
```csharp
// CORRECT ✅
.Join<Order, Customer>((o, c) => o.CustomerId == c.Id)
.Join<Order, Customer>((o, c) => o.CustomerId == c.Id && o.RegionId == c.RegionId)

// WRONG ❌
.Join<Order, Customer>((o, c) => o.Amount > c.CreditLimit)  // Not equality!
.Join<Order, Customer>((o, c) => o.CustomerId.StartsWith(c.IdPrefix))  // Not simple ==
```

---

### JOIN Constraint 3: One Side Must Be CTAS-Derived

**Rule**: At least one side of JOIN must be a CTAS-derived source (has aggregation/window)

**AI Action**:
```
If both sides are raw topics:
"For optimal join performance, at least one side should be a CTAS-derived source (aggregated/windowed)"
```

---

## Type Mapping Constraints

### Supported Types

| C# Type | Avro/Flink Type | Notes |
|---------|----------------|-------|
| `int` | `INT` | ✅ |
| `long` | `BIGINT` | ✅ |
| `float` | `FLOAT` | ✅ |
| `double` | `DOUBLE` | ✅ |
| `bool` | `BOOLEAN` | ✅ |
| `string` | `STRING` | ✅ |
| `DateTime` | `TIMESTAMP(3)` | ✅ |
| `DateTimeOffset` | `TIMESTAMP_LTZ(3)` | ✅ |
| `decimal` | `DECIMAL(p, s)` | ✅ Requires `[KafkaDecimal]` |
| `Guid` | `STRING` | ✅ |
| `byte` | `TINYINT` | ✅ |
| `short` | `SMALLINT` | ✅ |

### Unsupported Types

**AI Action**: If user uses unsupported type, suggest alternative

```csharp
// WRONG ❌
public TimeSpan Duration { get; set; }  // Not supported!

// CORRECT ✅
public long DurationMillis { get; set; }  // Use long for durations
```

---

## Configuration Constraints

### Scan Startup Mode (Validated)

**Rule**: `scan.startup.mode` must be one of the allowed values

**Allowed values**:
- `earliest-offset`
- `latest-offset`
- `group-offsets`
- `timestamp` (requires `scan.startup.timestamp-millis`)
- `specific-offsets` (requires `scan.startup.specific-offsets`)

**AI Action**:
```
If user specifies invalid scan.startup.mode:
"Allowed values: earliest-offset, latest-offset, group-offsets, timestamp, specific-offsets"
```

---

### Protected WITH Keys

**Rule**: These keys are reserved and cannot be overridden in `AdditionalProperties`:
- `connector`
- `topic`
- `format`
- `properties.bootstrap.servers`
- `schema.registry.url`

**AI Action**:
```
If user tries to override protected key in AdditionalProperties:
"This key is reserved. Use the corresponding configuration section instead."
```

---

## Validation Checklist for AI

Before suggesting Streaming DSL code:

- [ ] Entity has `[KafkaTopic]` attribute
- [ ] Decimal properties have `[KafkaDecimal]`
- [ ] If window query: `.FlinkSource()` with `EventTimeColumn` configured
- [ ] If window query: `GroupBy` includes `WindowStart` and `WindowEnd`
- [ ] If window query: `outputMode: Final` and `sinkMode: Upsert` are set
- [ ] If window + JOIN: Split into 2 separate queries
- [ ] If `OutputMode.Final`: Query has window
- [ ] If `SinkMode.Upsert`: Query has window + GroupBy, output entity has `[KafkaKey]`
- [ ] JOIN predicates are key equality only
- [ ] All types are supported
- [ ] `scan.startup.mode` is valid (if specified)

---

## Error Response Templates

### Template: Window + JOIN Violation

```
Error: Window queries cannot be combined with JOIN in a single query.

Solution: Split into 2 queries:

1. Window aggregation (CTAS):
   b.Entity<WindowedOrders>().ToQuery(q => q
       .From<Order>()
       .TumbleWindow(x => x.OrderTime, TimeSpan.FromMinutes(5))
       .GroupBy(x => new {
           x.CustomerId,
           WindowStart = FlinkWindow.Start(),
           WindowEnd = FlinkWindow.End()
       })
       .Select(...));

2. JOIN with windowed result:
   b.Entity<Result>().ToQuery(q => q
       .From<WindowedOrders>()
       .Join<WindowedOrders, Customer>((w, c) => w.CustomerId == c.Id)
       .Select(...));

See: docs/ai/PATTERNS.md#pattern-8-window--join-split-pattern
```

### Template: Missing WindowStart/WindowEnd

```
Error: Window queries must include WindowStart and WindowEnd in GroupBy.

Fix:
.GroupBy(x => new {
    x.YourKey,
    WindowStart = FlinkWindow.Start(),
    WindowEnd = FlinkWindow.End()
})
```

### Template: OutputMode.Final without Window

```
Error: OutputMode.Final is only supported for window queries.

Options:
1. Add a window:
   .TumbleWindow(x => x.Timestamp, TimeSpan.FromMinutes(5))

2. Change to Changelog:
   outputMode: StreamingOutputMode.Changelog
```

### Template: Missing EventTimeColumn

```
Error: Window queries require event-time configuration.

Fix:
b.Entity<YourEntity>().FlinkSource(s =>
    s.EventTimeColumn(x => x.YourTimestamp, watermarkDelay: TimeSpan.FromSeconds(5)));
```

---

**End of Constraints Reference**

For code patterns, see `docs/ai/PATTERNS.md`.
For complete guide, see `AI_DEVELOPMENT_GUIDE.md`.
