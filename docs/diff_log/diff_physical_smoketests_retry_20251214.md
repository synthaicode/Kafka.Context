# diff_physical_smoketests_retry_20251214

## 目的
Windows の physical smoke test が、起動直後の Kafka/Schema Registry/ksqlDB の揺れ等でフレークしないように、テスト側に最小限の retry を追加する。

## 変更概要
- `tests/physical/Kafka.Context.PhysicalTests/SmokeTests.cs` に `RunWithRetryAsync(...)` を追加し、`Provision_Add_Consume_And_Dlq` の全体を最大3回まで再実行する。
- retry 対象は「一時的になり得る例外」のみ（`OperationCanceledException` / `HttpRequestException` / `Confluent.Kafka.KafkaException` とその inner）。
- backoff は `2^attempt` 秒（上限10秒）。

## 影響範囲
- 物理テストの安定性のみ（プロダクションコードへの影響なし）。

