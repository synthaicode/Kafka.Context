# Contract: Kafka provisioning (AdminClient)

## 達成できる結果
起動時に topic/DLQ を「create-if-missing」で揃え、破綻を実行前に検出できる。

## 標準方針（移植元: Ksql.Linq `KafkaAdminService`）

### Kafka 接続検証
- AdminClient で metadata を取得し、broker が 0 の場合は起動失敗（fail fast）。

### Topic 作成ポリシー
- create-if-missing（存在していれば no-op）。
- 作成失敗は例外として伝播し、起動失敗（fail fast）。
- 期待する topic 設定が指定されている場合、既存 topic がそれと不一致なら起動失敗（fail fast）。
  - 判定範囲は完全一致とする（指定された partitions / replication factor / configs をすべて一致させる）。

### 作成パラメータの決定
- 既定: `NumPartitions=1`, `ReplicationFactor=1`
- 上書き: `KsqlDsl.Topics.<topicName>.Creation.*`（運用差分）
  - `NumPartitions`
  - `ReplicationFactor`
  - `Configs`（任意の topic config）
- topic 名の構造ルール（MVP）
  - `.pub` / `.int` サフィックスを持つ topic は、base topic（サフィックス無し）側に作成設定を置く。
  - `.pub` / `.int` 側で Creation を明示した場合はエラー（fail fast）。

### 作成設定の検索（補助ルール）
- `bar_1m_x_y` のような topic は prefix を遡って `KsqlDsl.Topics.<prefix>.Creation` を探す（最長一致）。
- `_hb_` を含む場合は `_` に正規化して検索する（hub 系の補正）。

### ReplicationFactor の扱い（開発/ローカル向け補正）
- `AdjustReplicationFactorToBrokerCount=true` の場合、broker 数より大きい RF は 1 に丸める。
- 作成時に RF 不正（InvalidReplicationFactor 等）で失敗した場合、RF=1 で 1 回だけリトライする。

## DLQ topic
- DLQ topic は 1 Context に 1 つ（詳細: `docs/contracts/dlq_topic.md`）。
- 未設定時の既定名: `dead_letter_queue`
- 自動作成を無効化できる（`DlqOptions.EnableAutoCreation=false` の場合は作成をスキップ）。
- 作成時は DLQ topic の既定 config（`cleanup.policy=delete`, `retention.ms=5000`）を適用する。
  - 追加/上書きしたい場合は `KsqlDsl.Topics.<dlqTopicName>.Creation.*` で指定する。

## Compacted topic（内部用途）
- compacted topic を create-if-missing で作成できる。
- `cleanup.policy=compact` 等の標準 config を適用する。
