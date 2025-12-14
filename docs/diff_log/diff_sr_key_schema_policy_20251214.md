# diff: SR key schema policy (2025-12-14)

## 変更理由
key が単一列の場合は Kafka の key serializer（primitive）を使える前提にし、SR に key schema を押し込まない運用にしたい。

## 変更内容
- Schema Registry の key subject `{topic}-key` は、**複合キー（`[KsqlKey]` が2つ以上）** のときだけ登録する。
- 単一 key / key 不在のときは `{topic}-key` を登録しない（value `{topic}-value` は従来どおり登録する）。

## 影響範囲
- Provisioning の SR 登録挙動のみ（runtime の Add/Consume は value 側 Avro のまま）。

