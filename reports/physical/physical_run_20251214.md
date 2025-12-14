# Physical test run report (2025-12-14, Windows)

## 実行環境
- OS: Windows
- Compose: `docs/environment/docker-compose.current.yml`
- Env: `KAFKA_CONTEXT_PHYSICAL=1`
- Test: `dotnet test tests/physical/Kafka.Context.PhysicalTests/Kafka.Context.PhysicalTests.csproj -c Release`

## 実行手順（再現条件）
1. `docker compose -f docs/environment/docker-compose.current.yml up -d`
2. `set KAFKA_CONTEXT_PHYSICAL=1`（PowerShell: `$env:KAFKA_CONTEXT_PHYSICAL='1'`）
3. `dotnet test tests/physical/Kafka.Context.PhysicalTests/Kafka.Context.PhysicalTests.csproj -c Release`

## 失敗ログ（発生順）

### 1) decimal field requires attribute
- Error: `System.InvalidOperationException : decimal field '...PhysicalOrder.Amount' requires [KsqlDecimalAttribute].`
- Location: `src/Kafka.Context.Infrastructure/SchemaRegistry/AvroSchemaBuilder.cs`

### 2) DLQ topic retention mismatch
- Error: `Topic 'dead_letter_queue' config 'retention.ms' mismatch. expected='5000', actual='604800000'`
- Location: `src/Kafka.Context.Infrastructure/Admin/KafkaAdminService.cs`

### 3) DlqEnvelope.Headers の Avro schema 未対応
- Error: `Unsupported property type 'Dictionary<string,string>' ... DlqEnvelope.Headers`
- Location: `src/Kafka.Context.Infrastructure/SchemaRegistry/AvroSchemaBuilder.cs`

### 4) ForEachAsync が呼び出しスレッドをブロック
- Symptom: `ForEachAsync(...)` から制御が戻らず、テストが進まずに `CancellationTokenSource(TimeSpan.FromSeconds(30))` が先に失効
- Location: `src/Kafka.Context.Infrastructure/Runtime/KafkaConsumerService.cs`

### 5) decimal のシリアライズ型不一致
- Error: `Unable to cast object of type 'System.Byte[]' to type 'Avro.AvroDecimal'. in field Amount`
- Location: `src/Kafka.Context.Infrastructure/Runtime/KafkaProducerService.cs`

## 対応と最終結果
- 上記の起因を修正し、`SmokeTests.Provision_Add_Consume_And_Dlq` が `PASS`。
- 確認観点:
  - (1) `ProvisionAsync` が Topic 作成/一致チェック + SR登録（valueは常に、keyは複合keyのみ）で停止しない
  - (2) `AddAsync` → `ForEachAsync` で 1件処理できる
  - (3) handler 例外が DLQ に入り、`KafkaContext.Dlq.ForEachAsync` で拾える

