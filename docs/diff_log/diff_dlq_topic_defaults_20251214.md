# diff_dlq_topic_defaults_20251214

## 背景
DLQ topic を自動作成する場合の“最低限動く”既定値を固定する。

## 決定事項
- DLQ topic 名の既定は `dead_letter_queue` とする。
- 自動作成時の既定は以下とする:
  - `retention.ms`: 5000ms
  - partitions: 1
  - replication factor: 1

## 未解決の論点（次アクション）
- 本番推奨の retention と cleanup.policy 等の推奨を別途定義するか。

