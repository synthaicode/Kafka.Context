# diff_topic_level_settings_20251214

## 背景
Provisioning（topic 作成/検証）や運用差分を、topic 単位で外部設定から制御できるようにする。

## 決定事項
- Topic 単位の設定は `KsqlDsl.Topics.<topicName>.*` に寄せる。
  - 例: `KsqlDsl.Topics.bar_1m.Creation.NumPartitions = 3`

## 影響
- Provisioning は `KsqlDsl.Topics` を参照して topic 作成パラメータを決定する。
- 既定値（DLQ の `NumPartitions` / `RetentionMs` など）と同様に、運用は appsettings/env で上書きできる。

