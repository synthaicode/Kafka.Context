# Contract: Public API surface (Kafka.Context / MVP)

## 達成できる結果
利用者が「覚えることが少ない」I/O API（`AddAsync` / `ForEachAsync`）で書ける。

## EventSet<T>.ForEachAsync（MVP）

### Simple handler
- `Task ForEachAsync(Func<T, Task> action)`
- `Task ForEachAsync(Func<T, Task> action, CancellationToken cancellationToken)`
- `Task ForEachAsync(Func<T, Task> action, TimeSpan timeout)`

### Handler with headers/meta
- `Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action)`
- `Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit)`
- `Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action, TimeSpan timeout)`

### Manual commit (when `autoCommit=false`)
- `void Commit(T entity)`

Note: MVP の manual commit は **record 単位のみ**。`Commit(entity)` は `ForEachAsync(..., autoCommit:false)` のハンドラ内で呼ぶ。

## KafkaContext constructors（MVP）
- `KafkaContext(KafkaContextOptions options)`
- `KafkaContext(KafkaContextOptions options, ILoggerFactory? loggerFactory)`
- `KafkaContext(IConfiguration configuration)`
- `KafkaContext(IConfiguration configuration, ILoggerFactory? loggerFactory)`
- `KafkaContext(IConfiguration configuration, string sectionName)`
- `KafkaContext(IConfiguration configuration, string sectionName, ILoggerFactory? loggerFactory)`

## DLQ read（MVP）
`KafkaContext` は 1 Context = 1 DLQ topic とし、`KafkaContext.Dlq` から読み取れる。

- `KafkaContext.Dlq`（type: `DlqSet`）
- `DlqSet.ForEachAsync(Func<DlqEnvelope, Task> action)`
- `DlqSet.ForEachAsync(Func<DlqEnvelope, Task> action, CancellationToken cancellationToken)`
- `DlqSet.ForEachAsync(Func<DlqEnvelope, Task> action, TimeSpan timeout)`
- `DlqSet.ForEachAsync(Func<DlqEnvelope, Dictionary<string, string>, MessageMeta, Task> action)`
- `DlqSet.ForEachAsync(Func<DlqEnvelope, Dictionary<string, string>, MessageMeta, Task> action, CancellationToken cancellationToken)`
- `DlqSet.ForEachAsync(Func<DlqEnvelope, Dictionary<string, string>, MessageMeta, Task> action, TimeSpan timeout)`

## KafkaContext provisioning（MVP）
- `Task ProvisionAsync(CancellationToken cancellationToken = default)`

## KafkaContextOptions（MVP）
```csharp
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace Kafka.Context.Application;

public class KafkaContextOptions
{
    public ISchemaRegistryClient SchemaRegistryClient { get; set; } = null!;
    public ILoggerFactory? LoggerFactory { get; set; }
    public IConfiguration? Configuration { get; set; }

    public bool AutoRegisterSchemas { get; set; } = true;
    public bool EnableCachePreWarming { get; set; } = true;
    public bool FailOnInitializationErrors { get; set; } = true;
    public bool FailOnSchemaErrors { get; set; } = true;
    public TimeSpan SchemaRegistrationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (SchemaRegistryClient == null)
            throw new InvalidOperationException("SchemaRegistryClient is required");

        if (SchemaRegistrationTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("SchemaRegistrationTimeout must be positive");
    }
}
```
