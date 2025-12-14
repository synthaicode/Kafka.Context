# diff_physical_smoketests_20251214

## 目的
Windows の physical テスト `SmokeTests.Provision_Add_Consume_And_Dlq` を実環境（Kafka + Schema Registry + ksqlDB）で通し、(1) ProvisionAsync, (2) AddAsync→ForEachAsync, (3) DLQ を確認可能にする。

## 変更概要
- Avro schema 生成で `Dictionary<string,string>` を `map<string>` として扱えるようにした（DLQ envelope の headers 用）。
- `KafkaConsumerService.ForEachAsync` が最初のメッセージ到着まで呼び出し元をブロックしないようにした（`await Task.Yield()`）。
- decimal の Avro logicalType(`decimal`) を `byte[]` ではなく `Avro.AvroDecimal` でシリアライズ/デシリアライズするようにした。
- physical smoke test の安定化:
  - `PhysicalOrder.Amount` に `[KsqlDecimal(9, 2)]` を付与
  - DLQ topic を固定名ではなく run ごとにユニーク名にする（既存クラスタ状態の影響を回避）

## 影響範囲
- `src/Kafka.Context.Infrastructure/SchemaRegistry/AvroSchemaBuilder.cs`: Avro map の追加サポート
- `src/Kafka.Context.Infrastructure/Runtime/KafkaConsumerService.cs`: ForEachAsync の非同期開始（ブロック回避）
- `src/Kafka.Context.Infrastructure/Runtime/KafkaProducerService.cs`: decimal の AvroDecimal 化
- `src/Kafka.Context.Infrastructure/Runtime/AvroGenericMapper.cs`: decimal/headers のマッピング補強
- `src/Kafka.Context.Infrastructure/Runtime/DlqProducerService.cs`: headers の map 変換
- `tests/physical/Kafka.Context.PhysicalTests/SmokeTests.cs`: decimal attribute + DLQ topic のユニーク化

## 補足（背景）
- 既存クラスタに `dead_letter_queue` が残っている場合、`retention.ms` の不一致で `ProvisionAsync` が fail-fast するため、テスト側で DLQ topic を run 毎に分離した。

