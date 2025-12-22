# GitHub Copilot Instructions for Kafka.Context

> **Note**: This file provides context-aware guidance for GitHub Copilot when working in this repository.

## Project Context

Kafka.Context is a C# library for Kafka + Schema Registry with an Entity Framework-like API.

**Key Files**:
- Comprehensive guide: `AI_DEVELOPMENT_GUIDE.md` (read this first!)
- Architecture: `ARCHITECTURE_REVIEW_STREAMING_DSL.md`
- Examples: `examples/quickstart/`, `examples/streaming-flink/`

## Quick Rules for Code Generation

### 1. Basic Kafka Operations (95% of use cases)

**Pattern**: Context + EventSet + AddAsync/ForEachAsync

```csharp
// Always use this pattern for simple produce/consume
[KafkaTopic("topic-name")]
public class Entity { /* properties */ }

public class EntityContext : KafkaContext
{
    public EventSet<Entity> Entities { get; set; } = null!;
    protected override void OnModelCreating(IModelBuilder b) => b.Entity<Entity>();
}
```

### 2. Streaming DSL (5% of use cases)

**Only suggest when user explicitly needs**:
- Window aggregations
- Stream JOINs
- SQL-like transformations

**Critical Constraints** (NEVER violate):
- ❌ Window + JOIN in same query → Split into 2 queries
- ✅ Window queries MUST include `WindowStart`/`WindowEnd` in GroupBy
- ✅ Window queries need `.FlinkSource()` with `EventTimeColumn`

### 3. Attributes to Always Add

```csharp
[KafkaTopic("topic-name")]      // REQUIRED on all entities
[KafkaKey]                      // For primary keys (upsert)
[KafkaDecimal(precision: 18, scale: 2)]  // For decimal properties
[KafkaTimestamp]                // For event-time columns
```

### 4. Error Handling in Production

Always suggest for production code:

```csharp
await context.Entities
    .OnError(ErrorAction.DLQ)
    .WithRetry(3)
    .ForEachAsync(async (entity, headers, meta) => {
        // Processing logic
    });
```

## Common Mistakes to Avoid

1. ❌ Using `DbSet<T>` instead of `EventSet<T>`
2. ❌ Combining window + JOIN in one query
3. ❌ Forgetting `[KafkaTopic]` attribute
4. ❌ Using `OutputMode.Final` without window
5. ❌ Missing `WindowStart`/`WindowEnd` in window GroupBy

## Decision Tree

```
User needs Kafka integration?
├─ Simple produce/consume? → Use Basic Pattern
└─ Windowing/JOIN/Aggregation? → Use Streaming DSL
    ├─ Window aggregation? → Check AI_DEVELOPMENT_GUIDE.md Section 2
    └─ JOIN + Window? → Split into 2 queries (constraint!)
```

## Reference

For detailed patterns, constraints, and examples, see: **`AI_DEVELOPMENT_GUIDE.md`**

## Examples Location

- Basic: `examples/quickstart/Program.cs`
- Streaming: `examples/streaming-flink/Program.cs`
- Flow: `examples/streaming-flink-flow/Program.cs`
