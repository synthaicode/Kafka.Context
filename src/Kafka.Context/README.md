# Kafka.Context

Kafka + Schema Registry を「Context中心・契約駆動」で扱うための、軽量で意見のある I/O ランタイム（契約形式: Avro）。

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

## Quick start

```csharp
using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

[KsqlTopic("orders")]
public sealed class Order
{
    public int Id { get; set; }
}

public sealed class AppContext : KafkaContext
{
    public AppContext(IConfiguration configuration, ILoggerFactory loggerFactory)
        : base(configuration, loggerFactory) { }

    public EventSet<Order> Orders { get; set; } = null!;

    protected override void OnModelCreating(IModelBuilder modelBuilder)
        => modelBuilder.Entity<Order>();
}

var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
await using var ctx = new AppContext(config, loggerFactory);

// Create/verify topics + register/verify Schema Registry subjects (fail-fast).
await ctx.ProvisionAsync();

// Produce.
await ctx.Orders.AddAsync(new Order { Id = 1 });

// Consume.
await ctx.Orders.ForEachAsync(o =>
{
    Console.WriteLine($"Order: {o.Id}");
    return Task.CompletedTask;
});
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

`KsqlDsl.Topics.<topicName>.Consumer.*` / `KsqlDsl.Topics.<topicName>.Producer.*` で topic 別に Confluent.Kafka の設定を上書きできます。

## Preview Avro before Schema Registry registration

```csharp
var plans = ctx.PreviewSchemaRegistryAvro();
foreach (var p in plans)
    Console.WriteLine($"{p.ValueSubject} => {p.ExpectedValueRecordFullName}");
```

