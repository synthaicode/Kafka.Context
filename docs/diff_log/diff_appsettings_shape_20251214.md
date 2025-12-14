# diff_appsettings_shape_20251214

## 背景
Kafka.Context の設定導入を最短にし、既存の Ksql.Linq 側の運用知見を引き継ぐため、appsettings の形を固定する。

## 決定事項
- 設定セクション名は `KsqlDsl` を採用する（.NET 標準の `IConfiguration` を前提）。
- 最小設定は Kafka 接続（`Common`）+ Schema Registry（`SchemaRegistry.Url`）+ DLQ（`DlqTopicName` / `DeserializationErrorPolicy`）とする。
- `KsqlDbUrl` は本 OSS の Non-Goals により標準設定から除外する（必要になった場合は別途差分で追加検討する）。

## 参照
- `docs/contracts/appsettings.md`

