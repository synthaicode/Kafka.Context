# Kafka.Context

A lightweight, opinionated Kafka + Schema Registry runtime with a context-centric, contract-first workflow (Avro).

Source & docs: https://github.com/synthaicode/Kafka.Context
appsettings.json guide (EN): https://github.com/synthaicode/Kafka.Context/blob/main/docs/contracts/appsettings.en.md

This API is intentionally small.

- Produce / Consume only
- Explicit failure handling via DLQ
- Avro + Schema Registry are used to keep schemas reusable as contracts for downstream systems (e.g., Flink)

Single-column key is treated as Kafka primitive and is not registered to SR. Only composite keys are treated as schema contracts.

## Non-Goals

- No stream processing engine (Kafka Streams/Flink-style processing is out of scope)
- No ksqlDB query generation / DSL
- No general-purpose Kafka client wrapper (this package stays opinionated and small)
- No automatic schema evolution/migration tooling beyond (register/verify) during provisioning
- No “external Schema Registry schema → POCO” mapping layer (consume assumes the Avro contract matches your POCO)

## Install

```sh
dotnet add package Kafka.Context
```

## Minimal configuration

`appsettings.json`:
```json
{
  "KsqlDsl": {
    "Common": { "BootstrapServers": "127.0.0.1:39092", "ClientId": "my-app" },
    "SchemaRegistry": { "Url": "http://127.0.0.1:18081" },
    "DlqTopicName": "dead_letter_queue"
  }
}
```

## EF-style quick start (KafkaContext as DbContext)

```csharp
using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

[KafkaTopic("orders")]
public sealed class Order
{
    public int Id { get; set; }
}

// EF-like: KafkaContext ~= DbContext, EventSet<T> ~= DbSet<T>
public sealed class AppKafkaContext : KafkaContext
{
    public AppKafkaContext(IConfiguration configuration, ILoggerFactory loggerFactory)
        : base(configuration, loggerFactory) { }

    public EventSet<Order> Orders { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
        => modelBuilder.Entity<Order>();
}

var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
await using var ctx = new AppKafkaContext(config, loggerFactory);

// Create/verify topics + register/verify Schema Registry subjects (fail-fast).
await ctx.ProvisionAsync();

// Produce (EF-ish: AddAsync).
await ctx.Orders.AddAsync(new Order { Id = 1 });

// Consume (EF-ish: ForEachAsync).
await ctx.Orders.ForEachAsync(o =>
{
    Console.WriteLine($"Order: {o.Id}");
    return Task.CompletedTask;
});
```

## EF-style mapping tip (KafkaTopic on entity)

`KafkaTopicAttribute` is the equivalent of mapping an entity to a table name:

```csharp
[KafkaTopic("orders")]
public sealed class Order { /* ... */ }
```

## Retry / DLQ

```csharp
await ctx.Orders
    .WithRetry(maxRetries: 2, retryInterval: TimeSpan.FromMilliseconds(200))
    .OnError(ErrorAction.DLQ)
    .ForEachAsync(_ => throw new InvalidOperationException("boom"));

await ctx.Dlq.ForEachAsync((env, headers, meta) =>
{
    Console.WriteLine($"DLQ: {env.Topic} {env.ErrorType} {env.ErrorFingerprint}");
    return Task.CompletedTask;
});
```

## Per-topic consumer/producer config

You can override Confluent.Kafka settings per topic via:
- `KsqlDsl.Topics.<topicName>.Consumer.*`
- `KsqlDsl.Topics.<topicName>.Producer.*`

## Preview Avro before Schema Registry registration

```csharp
var plans = ctx.PreviewSchemaRegistryAvro();
foreach (var p in plans)
    Console.WriteLine($"{p.KeySubject} / {p.ValueSubject}");
    Console.WriteLine($"{p.ValueSubject} => {p.ExpectedValueRecordFullName}");
```
