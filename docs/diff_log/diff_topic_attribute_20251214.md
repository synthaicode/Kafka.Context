# diff_topic_attribute_20251214

## 背景
Provisioning（topic 作成/検証）の入力を「コード（モデル）」からも与えられるようにし、最短で動く導入体験を作る。

## 決定事項
- topic 情報は `KsqlTopicAttribute` で設定可能とする。
  - `Name`（必須）
  - `PartitionCount`（既定 1）
  - `ReplicationFactor`（既定 1）
- 運用差分は `KsqlDsl.Topics.<topicName>.Creation.*` を優先し、attribute は既定値として扱う。

## 影響
- Provisioning は「attribute と appsettings をマージ」して作成パラメータを決定する。
- attribute だけでも最小構成で動かせるが、本番は `KsqlDsl.Topics` での制御を推奨する。

