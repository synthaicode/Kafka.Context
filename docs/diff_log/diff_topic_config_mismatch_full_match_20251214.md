# diff_topic_config_mismatch_full_match_20251214

## 背景
Provisioning の「topic 設定不一致」を曖昧にすると、環境差分で事故りやすい。

## 決定事項
- `KsqlDsl.Topics.<topicName>.Creation.*` が指定されている場合、既存 topic の設定は完全一致を要求する。
  - partitions / replication factor / configs をすべて一致させる。

