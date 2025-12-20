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
- `Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit, CancellationToken cancellationToken)`
- `Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action, TimeSpan timeout)`

### Manual commit (when `autoCommit=false`)
- `void Commit(MessageMeta meta)`

Note: MVP の manual commit は **record 単位のみ**。`Commit(meta)` は `ForEachAsync(..., autoCommit:false)` のハンドラ内で呼ぶ。

## EventSet<T>.AddAsync（MVP）
- `Task AddAsync(T entity)`
- `Task AddAsync(T entity, CancellationToken cancellationToken)`
- `Task AddAsync(T entity, Dictionary<string, string> headers)`
- `Task AddAsync(T entity, Dictionary<string, string> headers, CancellationToken cancellationToken)`

Note: `ForEachAsync` の POCO マッピングは「受信した Avro record の field 名/型が POCO と一致する」ことを前提とする。外部システムが作った schema を POCO に寄せる mapping 層は Non-Goals。

## DynamicTopicSet (SR-driven consume)

### Create by topic name
- `DynamicTopicSet Topic(string topic)`

### Consume with key/value
- `Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action)`
- `Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit)`
- `Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit, CancellationToken cancellationToken)`
- `Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, CancellationToken cancellationToken)`
- `Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, TimeSpan timeout)`

### Manual commit (when `autoCommit=false`)
- `void Commit(MessageMeta meta)`

### Key contract (when consuming external systems)
- If the key is Confluent Avro payload, `key` is `GenericRecord` (e.g., window boundaries for tumbling/hopping results).
- Otherwise, `key` is `byte[]` (SR-unregistered primitive/unknown encoding).

Note: ksqlDB windowed keys may append window bytes after the Confluent Avro payload. In that case `key` is `WindowedKey` (contains the decoded Avro `Key` plus extracted window boundary info).

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
