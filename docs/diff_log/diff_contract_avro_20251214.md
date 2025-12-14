# diff_contract_avro_20251214

## 背景
Kafka.Context の「契約駆動」を成立させるため、Schema Registry 上の契約形式を先に確定する。

## 決定事項
- 契約形式（schema format）は Avro を採用する。
- SerDes は Confluent 公式（`Confluent.SchemaRegistry.Serdes`）を前提とする。

## 影響
- Provisioning は Topic に加えて Schema Registry 登録/検証を必須化する。
- 互換性・命名規約の不一致は「起動失敗（fail fast）」に寄せる。

## 未解決の論点（次アクション）
- Subject 命名規約（RecordNameStrategy か topic ベースか）
- 互換性レベル（例: BACKWARD/FULL/TRANSITIVE）を Context が設定するか、検証のみか
- DLQ 格納形式（raw payload + headers/meta の標準化範囲）

## Update（後続diffで解決）
- Subject 命名: `docs/diff_log/diff_sr_subject_naming_20251214.md`
- 互換性レベル: `docs/diff_log/diff_sr_compatibility_policy_20251214.md`
- DLQ 形式: `docs/diff_log/diff_dlq_envelope_20251214.md` / `docs/diff_log/diff_dlq_no_raw_payload_20251214.md`
