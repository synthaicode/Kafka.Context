# diff_dlq_topic_inherit_cluster_defaults_20251214

## 背景
DLQ topic は短期退避/運用補助であり、環境ごとに望ましい保持やポリシーが変わるため、既定の topic config をライブラリ側で固定しない方針を明確化する。

## 決定事項
- DLQ topic の topic config（例: `retention.ms`, `cleanup.policy`）はクラスタ既定値に従う。
- 追加設定が必要な場合は、DLQ topic 名で `KsqlDsl.Topics.<dlqTopicName>.Creation.*` に記載して上書きする方式とする。

## Update（後続diffで方針更新）
- DLQ topic は推奨 config をデフォルト適用: `docs/diff_log/diff_dlq_topic_default_recommended_configs_20251214.md`
