# diff_schema_registry_avro_preview_20251214

## 目的
Schema Registry へ登録する予定の Avro（key/value subject と schema JSON）を、登録前にローカルで確認できるようにする。

## 変更概要
- `KafkaContext.PreviewSchemaRegistryAvro()` を追加し、`ProvisionAsync` が対象とする全 topic（entity + DLQ）の Avro を一覧で返す。
- 戻り値 `SchemaRegistryAvroPlan` には以下を含める:
  - `topicName` / `*-key` / `*-value` の subject 名
  - key schema（複合 key のときのみ）
  - value schema（常に）
  - value schema の record fullname（Provision 時の整合チェックに使う値）

## 使い方（例）
- `var plans = ctx.PreviewSchemaRegistryAvro();`
- `plans` をログ/スナップショットに保存して SR 変更前にレビューする。

