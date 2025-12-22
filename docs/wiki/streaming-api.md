# Kafka.Context.Streaming — API Notes (English)

This page describes how to **declare streaming queries in `OnModelCreating`** and register them via **`ProvisionStreamingAsync()`**.

## Goals

- Keep the modeling entry point the same as `KafkaContext` (`OnModelCreating`).
- Reuse the familiar `EventSet<T>` experience for “attach to existing topics”.
- Declare derived outputs via `ToQuery(...)` without making the consumer side (`ForEachAsync`) responsible for output semantics.

## Key Concepts

### Input (attach) vs derived output (provision)

- **Attach** means: the topic already exists, and you only read/write `EventSet<T>` from/to that topic.
- **Provision (Streaming)** means: you declare derived outputs in `OnModelCreating` using `ToQuery(...)`, and then register them by calling `ProvisionStreamingAsync()`.

### `ToQuery(...)` is a declaration

`ToQuery(...)` stores a query definition in a context-scoped registry. Nothing is executed until you call:

```csharp
await ctx.ProvisionStreamingAsync();
```

### `OutputMode` (per-query)

`StreamingOutputMode` is a per-query switch that expresses your intent:

- `Changelog` (default): normal streaming output.
- `Final`: “final result only” intent. For now it is **allowed only for window queries (TUMBLE/HOP)** and fails fast otherwise.

Note: Flink SQL does not have `EMIT CHANGES/FINAL` clauses like ksqlDB; this flag is treated as **intent + constraints**.

### `SinkMode` (per-query)

`StreamingSinkMode` is a per-query hint about the expected sink/topic behavior:

- `AppendOnly` (default): append-only output.
- `Upsert`: keyed updates are expected (infra-dependent; topic compaction / upsert sink, etc.).

For MVP:

- `Upsert` is allowed only for **window + CTAS (TABLE)** queries and fails fast otherwise.

## Minimal Example (Flink)

Declare sources and derived outputs in `OnModelCreating`:

```csharp
protected override void OnModelCreating(IModelBuilder b)
{
    // Attach to existing input topics
    b.Entity<Order>();
    b.Entity<Customer>();

    // Derived output (INSERT)
    b.Entity<OrderSummary>().ToQuery(q => q
        .From<Order>()
        .Join<Order, Customer>((o, c) => o.CustomerId == c.Id)
        .Where((o, c) => c.IsActive)
        .Select((o, c) => new OrderSummary { CustomerId = o.CustomerId, CustomerName = c.Name }));

    // (Flink) declare source event-time + watermark for windowing
    b.Entity<WindowInput>().FlinkSource(s =>
        s.EventTimeColumn(x => x.EventTime, watermarkDelay: TimeSpan.FromSeconds(5)));

    // Derived output (Window / TABLE semantics)
    b.Entity<TumbleAggByCustomer>().ToQuery(q => q
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
        }),
        outputMode: StreamingOutputMode.Final,
        sinkMode: StreamingSinkMode.Upsert);
}
```

Then register (provision) derived outputs:

```csharp
await ctx.ProvisionStreamingAsync();
```

## Producing and Consuming Data

The consumer side does not try to interpret “final vs changes”; it simply reads from the topic.

```csharp
// Produce input events
await ctx.Set<Order>().AddAsync(new Order { /* ... */ }, cancellationToken);

// Consume derived output events (from the derived output topic)
await ctx.Set<OrderSummary>().ForEachAsync(
    item => { /* ... */ },
    cancellationToken);
```

## Flink: Window Outputs and “Final”

In Flink, windowed aggregations are finalized based on watermark progression and lateness policies.
If you want “final-like” behavior for downstream consumers, that is typically achieved via **sink/topic strategy** (e.g., upsert sink + compaction).

`sinkMode: Upsert` is a hint for that strategy; actual provisioning of Kafka table connectors is infra-dependent.

## Flink: Source/Sink DDL generation

If your dialect provider supports catalog DDL generation (Flink does), `ProvisionStreamingAsync()` can emit and execute:

- `CREATE TABLE IF NOT EXISTS ... WITH ('connector'='kafka', ...)` for input topics (sources)
- `CREATE TABLE IF NOT EXISTS ... WITH ('connector'='kafka', ...)` for derived output topics (sinks)
- `INSERT INTO ... SELECT ...` for query jobs

Configure connector properties via `FlinkKafkaConnectorOptions` when creating `FlinkDialectProvider`.
The generated Flink Kafka tables currently support **Avro only** (`format = 'avro-confluent'`).
Use `kafka-context streaming flink with-preview` (CLI) to preview the merged `WITH (...)` properties before provisioning.
If you pass `--assembly`, the CLI can include column definitions in the DDL output (by matching `[KafkaTopic]` POCOs).

## Flink-specific functions (LINQ surface)

The following APIs are Flink-only entry points used in `ToQuery(...)`. They are mapped by the Flink dialect renderer.

### Window + Aggregates
- `FlinkWindow.Start()` / `FlinkWindow.End()` / `FlinkWindow.Proctime()` (window bounds + processing time)
- `FlinkAgg.Count()` / `FlinkAgg.Sum(...)` / `FlinkAgg.Avg(...)`

### Strings / Regex / LIKE
- `FlinkSql.Concat(...)`, `FlinkSql.Locate(...)`, `FlinkSql.Position(...)`
- `FlinkSql.Lpad(...)`, `FlinkSql.Rpad(...)`, `FlinkSql.SplitIndex(...)`
- `FlinkSql.RegexpExtract(...)`, `FlinkSql.RegexpReplace(...)`
- `FlinkSql.SimilarTo(...)`, `FlinkSql.Regexp(...)`, `FlinkSql.Like(...)`

### JSON
- `FlinkSql.JsonValue(...)`, `FlinkSql.JsonQuery(...)`, `FlinkSql.JsonExists(...)`

### Date/Time
- `FlinkSql.ToTimestamp(...)`, `FlinkSql.ToTimestampLtz(...)`, `FlinkSql.FromUnixtime(...)`, `FlinkSql.CurrentTimestamp()`
- `FlinkSql.DateFormat(...)`, `FlinkSql.Extract(...)`, `FlinkSql.TimestampAdd(...)`, `FlinkSql.TimestampDiff(...)`
- `FlinkSql.DayOfWeek(...)`, `FlinkSql.DayOfYear(...)`, `FlinkSql.WeekOfYearExtract(...)`, `FlinkSql.WeekOfYearFormat(...)`
- `FlinkSql.FloorTo(...)`, `FlinkSql.Interval(...)`, `FlinkSql.DateLiteral(...)`, `FlinkSql.AddInterval(...)`, `FlinkSql.SubInterval(...)`
- `FlinkSql.ToUtcTimestamp(...)`, `FlinkSql.FromUtcTimestamp(...)`

### Cast / Null / UUID
- `FlinkSql.Cast<T>(...)`, `FlinkSql.TryCast<T>(...)`
- `FlinkSql.IfNull(...)`, `FlinkSql.NullIf(...)`
- `FlinkSql.Uuid()`

### Arrays / Maps
- `FlinkSql.Array(...)`, `FlinkSql.ArrayLength(...)`, `FlinkSql.ArrayElement(...)`
- `FlinkSql.ArrayContains(...)`, `FlinkSql.ArrayDistinct(...)`, `FlinkSql.ArrayJoin(...)`
- `FlinkSql.Map(...)`, `FlinkSql.MapKeys(...)`, `FlinkSql.MapValues(...)`, `FlinkSql.MapGet(...)`

### appsettings.json: `KsqlDsl:Streaming:Flink:Sources`

Use this section to organize Flink `WITH (...)` properties per Kafka topic (source tables).

### Optional typed shortcuts (recommended for common keys)

To reduce typos in frequently used keys, these shortcuts exist under `KsqlDsl:Streaming:Flink`:

- `ScanStartupMode` → `scan.startup.mode` (global default)
- `SourceGroupIdByTopic` → `properties.group.id` (per-topic sources)

These shortcuts are merged into the effective `WITH (...)` map. If you also define the same key in `With/Sources`, values must match.

### `scan.startup.mode` allowed values (validated)

For Flink source tables, this library validates `scan.startup.mode` to reduce production misconfiguration.

Allowed values:
- `earliest-offset`
- `latest-offset`
- `group-offsets`
- `timestamp` (requires `scan.startup.timestamp-millis`)
- `specific-offsets` (requires `scan.startup.specific-offsets`)

### Security templates (SASL/SSL)

Put Kafka client security under `KsqlDsl:Common:AdditionalProperties` using Flink Kafka connector keys (`properties.*`).

SASL_SSL (SCRAM-SHA-512) example:

```json
{
  "KsqlDsl": {
    "Common": {
      "AdditionalProperties": {
        "properties.security.protocol": "SASL_SSL",
        "properties.sasl.mechanism": "SCRAM-SHA-512",
        "properties.sasl.jaas.config": "org.apache.kafka.common.security.scram.ScramLoginModule required username='user' password='pass';",
        "properties.ssl.truststore.location": "/path/to/truststore.jks",
        "properties.ssl.truststore.password": "changeit"
      }
    }
  }
}
```

SSL (mTLS) example:

```json
{
  "KsqlDsl": {
    "Common": {
      "AdditionalProperties": {
        "properties.security.protocol": "SSL",
        "properties.ssl.truststore.location": "/path/to/truststore.jks",
        "properties.ssl.truststore.password": "changeit",
        "properties.ssl.keystore.location": "/path/to/keystore.jks",
        "properties.ssl.keystore.password": "changeit",
        "properties.ssl.key.password": "changeit"
      }
    }
  }
}
```

### Recommended placement rules (to keep it easy)

Use this simple rule set to avoid “where should I put this key?” confusion:

1) Put **cluster-wide connector properties** in `KsqlDsl:Streaming:Flink:With`.
2) Put **topic-specific source tweaks** in `KsqlDsl:Streaming:Flink:Sources:<topic>`.
3) Put **topic-specific sink tweaks** in `KsqlDsl:Streaming:Flink:Sinks:<topic>`.
4) If you define the **same key** in `With` and `Sources/Sinks`, the per-topic value takes precedence. Other duplicates fail fast.

#### Common keys and where to place them

| Key (WITH) | Typical meaning | Recommended place |
|---|---|---|
| `properties.bootstrap.servers` | Kafka brokers | `KsqlDsl:Common:BootstrapServers` (auto-mapped) |
| `schema.registry.url` | Schema Registry URL | `KsqlDsl:SchemaRegistry:Url` (auto-mapped) |
| `scan.startup.mode` | Source startup position | `KsqlDsl:Streaming:Flink:With` (global) or `Sources:<topic>` (override) |
| `properties.group.id` | Consumer group id (source) | `Sources:<topic>` |
| `properties.security.protocol` / `properties.sasl.*` / `properties.ssl.*` | Kafka client security | `KsqlDsl:Common:AdditionalProperties` (auto-mapped) or `KsqlDsl:Streaming:Flink:With` |
| `sink.parallelism` | Sink parallelism hint | `Sinks:<topic>` |

Example:

```json
{
  "KsqlDsl": {
    "Streaming": {
      "Flink": {
        "ScanStartupMode": "group-offsets",
        "SourceGroupIdByTopic": {
          "orders": "orders-consumer"
        },
        "With": {
          "scan.startup.mode": "group-offsets"
        },
        "Sources": {
          "orders": {
            "scan.startup.mode": "earliest-offset"
          }
        },
        "Sinks": {
          "tumble-agg-by-customer": {
            "sink.parallelism": "1"
          }
        }
      }
    }
  }
}
```

## Current Constraints (MVP)

### Loop guard (required)

If an output topic is also used as an input source topic, provisioning fails fast to prevent accidental self-loops.

### CTAS vs INSERT (simplified)

- If the plan has `GroupBy` / aggregate / `Having` → treated as **TABLE (CTAS)**.
- Otherwise → treated as **STREAM (INSERT)**.

### Window constraints

- Window queries must be CTAS (TABLE).
- Window queries must be single-source (no JOIN).
- GroupBy must include window bounds (`FlinkWindow.Start()` and `FlinkWindow.End()`).

### Join constraints

For now, JOIN is intentionally restricted:

- JOIN is allowed only when **exactly one side is a CTAS-derived source**.
- JOIN type is **INNER only**.
- `ON` predicate must be **key equality only** (conjunctions of `==` between member accesses).

## C# -> Avro mapping (Flink-compatible subset)

This project keeps only the attributes that are valid across Kafka + Schema Registry + Flink usage.
ksqlDB-only concepts are intentionally excluded.

### Supported attributes

- `KafkaTopicAttribute` (class)
  - Binds the class to a Kafka topic name.
  - Used as the default topic name for provisioning.
- `KafkaKeyAttribute` (property)
  - Marks key fields.
  - Used for Upsert PK generation in Flink sinks.
- `KafkaTimestampAttribute` (property)
  - Marks the event-time column on the POCO.
  - Kept to support future ToQuery-based conversions.
- `KafkaDecimalAttribute` (property)
  - Maps decimal to Avro logical type (`decimal` with precision/scale).
- `SchemaSubjectAttribute` (class/struct)
  - Explicit Schema Registry subject name (when provided).
- `SchemaFingerprintAttribute` (class/struct)
  - Embedded fingerprint for SR verification workflows.

### Excluded (ksqlDB-specific / not used)

- `KsqlTable`, `KsqlIgnore`, and other ksqlDB-only attributes are not used here.

### Notes

- SR registration flows from C# types (POCOs) to schemas.
- DDL generation uses C# types, not SR, as the source of truth.
- Schema Registry auto-register on produce is **always OFF**. Schemas must be registered explicitly before producing.

## Operational assumptions (by scenario)

The runtime is split across processes. Use the following scenario-based flows.

### Scenario A: Existing SR + existing topics (ForEachAsync / AddAsync)

Use this when the Kafka topics and SR subjects already exist.

1) Use the CLI to scaffold POCOs from SR and add them to your source tree (contracts project).

```powershell
kafka-context schema scaffold --sr-url http://127.0.0.1:18081 --subject orders-value --output ./Contracts
```

Add the generated file to your project/solution.
2) Use `EventSet<T>` for `AddAsync` / `ForEachAsync` only (no DDL).
3) Keep `AutoRegisterSchemas` **OFF** (SR is already registered).

### Scenario B: Existing SR + new streaming queries (CTAS/INSERT)

Use this when inputs already exist in SR but you are adding new derived outputs.

1) Ensure POCOs for inputs exist (from SR scaffold or shared contracts).
2) Define `ToQuery(...)` in `OnModelCreating`.
3) Run `ProvisionStreamingAsync()` to emit Flink DDL:
   - source/sink `CREATE TABLE ... WITH (...)`
   - query `INSERT INTO ... SELECT ...`
4) Flink runtime executes the queries and writes to output topics.
5) Consumers read outputs with `EventSet<T>` / `ForEachAsync`.

### Scenario C: New topics + new schemas (full provisioning)

Use this when topics and SR entries must be created from C#.

1) Define POCOs and attributes (`KafkaTopic`, `KafkaKey`, etc.).
2) Run `ProvisionAsync()` to create topics and register SR schemas.
3) Run `ProvisionStreamingAsync()` to emit Flink DDL for sources/sinks/queries.
4) Start Flink jobs (CTAS/INSERT runtime).
5) Produce/consume with `EventSet<T>` / `ForEachAsync`.

### Policy summary

- **AutoRegisterSchemas is always OFF**.
- SR registration happens **before** DDL/produce.
- DDL generation uses **C# types as the source of truth**, not SR.

## Examples

- `examples/streaming-flink/Program.cs`
- `examples/streaming-flink-flow/Program.cs`
