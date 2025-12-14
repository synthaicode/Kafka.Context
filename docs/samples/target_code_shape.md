# Target code shape (MVP)

この OSS が目指す利用者体験（Context中心・最小 I/O API）を示す。
このページは「実装済みコード」ではなく「目標の形」を固定するためのサンプルである。

## Minimal sample

```csharp
using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Kafka.Context.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

[KsqlTopic("orders")]
public class Order
{
    public int Id { get; set; }

    [KsqlDecimal(precision: 18, scale: 2)]
    public decimal Amount { get; set; }
}

public class OrderContext : KsqlContext
{
    public OrderContext(KsqlContextOptions options) : base(options.Configuration!, options.LoggerFactory) { }
    public OrderContext(IConfiguration configuration, ILoggerFactory? loggerFactory = null) : base(configuration, loggerFactory) { }

    public EventSet<Order> Orders { get; set; }

    protected override void OnModelCreating(IModelBuilder modelBuilder) { }
}

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        await using var context = new OrderContext(configuration, LoggerFactory.Create(b => b.AddConsole()));

        var order = new Order
        {
            Id = Random.Shared.Next(),
            Amount = -42.5m
        };

        await context.Orders.AddAsync(order);
        await Task.Delay(500);

        await context.Orders
            .OnError(ErrorAction.DLQ)
            .WithRetry(3)
            .ForEachAsync(o =>
            {
                if (o.Amount < 0)
                {
                    throw new InvalidOperationException("Amount cannot be negative");
                }
                Console.WriteLine($"Processed order {o.Id}: {o.Amount}");
                return Task.CompletedTask;
            });
    }
}
```

## 意図（MVP）
- I/O は `AddAsync` / `ForEachAsync` に畳む。
- 失敗の取り回し（Retry/DLQ）を builder 連鎖で完結させる。
- 契約は Avro + Schema Registry を前提にし、Provisioning で fail fast する。

## Manual commit sample
```csharp
using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Kafka.Context.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

[KsqlTopic("manual-commit-orders")]
public class ManualCommitOrder
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}

public class ManualCommitContext : KafkaContext
{
    public ManualCommitContext(KafkaContextOptions options) : base(options.Configuration!, options.LoggerFactory) { }
    public ManualCommitContext(IConfiguration configuration, ILoggerFactory? loggerFactory = null) : base(configuration, loggerFactory) { }
    public EventSet<ManualCommitOrder> Orders { get; set; }
    protected override void OnModelCreating(IModelBuilder modelBuilder) { }
}

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        await using var context = new ManualCommitContext(configuration, LoggerFactory.Create(b => b.AddConsole()));

        var order = new ManualCommitOrder
        {
            OrderId = Random.Shared.Next(),
            Amount = 10m
        };

        await context.Orders.AddAsync(order);
        await Task.Delay(500);

        await context.Orders.ForEachAsync(async (order, headers, meta) =>
        {
            Console.WriteLine($"Processing order {order.OrderId}: {order.Amount}");
            context.Orders.Commit(order);
        }, autoCommit: false);
    }
}
```
