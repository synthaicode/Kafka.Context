# diff_physical_crossprocess_runner_20251214

## 目的
物理テストで `AddAsync`（producer）と `ForEachAsync`（consumer）を別プロセス起動し、Schema Registry 経由の Avro が成立することを安定して確認する。

## 変更概要
- `tests/physical/Kafka.Context.PhysicalRunner` に依存パッケージを追加（`ConfigurationBuilder` / `LoggerFactory` を解決）。
- runner は起動時に `ProvisionAsync` を実行し、topic/SR の事前準備を行う。
- consumer 側は `KsqlDsl.Topics.<topic>.Consumer.AutoOffsetReset=Earliest` を使用（前後関係の揺れに耐える）。
- broker の `auto.create.topics.enable=true` 環境では、AdminClient の metadata 要求で topic がデフォルト設定で自動作成され得るため、runner の cross-process 用 topic は運用差分（Creation.Configs）での厳密一致を前提にしない構成にしている。

