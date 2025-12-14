# diff_dlq_topic_default_recommended_configs_20251214

## 背景
DLQ topic の運用は環境差分が出やすいため、最低限の推奨 config を「デフォルト値」としてライブラリ側で適用する。

## 決定事項
- DLQ topic は自動作成時に推奨 config をデフォルト値として適用する。
  - `cleanup.policy=delete`
  - `retention.ms=5000`
- 追加/上書きは `KsqlDsl.Topics.<dlqTopicName>.Creation.Configs` で行う。

## 補足
- 以前の「DLQ topic config はクラスタ既定に委譲する」方針を更新する。

