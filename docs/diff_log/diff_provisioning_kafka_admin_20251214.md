# diff_provisioning_kafka_admin_20251214

## 背景
MVP の Provisioning（Topic 作成/検証）を実装するため、Kafka AdminClient による create-if-missing の挙動を固定する。

## 決定事項
- Provisioning の Kafka 側は Confluent AdminClient を使い、topic を create-if-missing で揃える。
- 作成パラメータは `KsqlDsl.Topics.<topicName>.Creation.*` を優先し、`.pub/.int` は base topic 側に設定を置く。
- RF は（任意で）broker 数に合わせて補正し、RF 不正時は RF=1 で 1 回だけリトライする。
- DLQ topic の作成も同一パスで扱う（`DlqOptions` を反映）。

## 参照
- `docs/contracts/provisioning_kafka.md`

