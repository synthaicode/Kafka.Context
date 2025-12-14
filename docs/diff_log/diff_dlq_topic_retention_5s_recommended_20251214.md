# diff_dlq_topic_retention_5s_recommended_20251214

## 背景
DLQ は raw payload を持たない方針のため、DLQ topic の運用（retention）を明示しないと長期保管・監査用途に誤用されやすい。

## 決定事項
- DLQ topic の `retention.ms` 既定値は 5000ms とする。
- 5000ms は本番推奨値として扱う（長期保管は別経路で行う）。
- 以降の値は appsettings（`KsqlDsl.DlqOptions.RetentionMs`）で上書きする。

## 未解決の論点（次アクション）
- cleanup.policy 等の推奨を標準化するか（現状は `KsqlDsl.DlqOptions.AdditionalConfigs` に委譲）。

## Update（後続diffで方針更新）
- DLQ topic は推奨 config をデフォルト適用: `docs/diff_log/diff_dlq_topic_default_recommended_configs_20251214.md`
