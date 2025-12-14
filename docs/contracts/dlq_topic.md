# Contract: DLQ topic policy (Kafka.Context)

## 達成できる結果
DLQ の配置（topic）と運用パラメータを固定し、環境差分で壊れないようにする。

## 標準方針（移植元: Ksql.Linq 実装）

### 1 context = 1 DLQ topic
- 1 Context に 1 つの共通 DLQ topic を持つ。
- 元 topic は `DlqEnvelope.Topic` に保持する。

### 命名
- 強制命名規約は設けない（設定が優先）。
- 未設定時の既定: `dead_letter_queue`

### topic 作成（provisioning）
- DLQ topic は「推奨 config」をデフォルト値として適用して自動作成する。
- 追加/上書きが必要な場合は、DLQ topic 名で `KsqlDsl.Topics.<dlqTopicName>.Creation.*` に記載して上書きする。
- 自動作成する場合の既定:
  - partitions: 1
  - replication factor: 1
  - configs:
    - `cleanup.policy=delete`
    - `retention.ms=5000`

### Schema Registry
- ランタイム初期化（Schema 登録パス）では DLQ も EntityModel として扱い、SR 登録対象に含める。
- デザイン時（スクリプト生成/スキーマエクスポート）では DLQ を除外する。

## 未解決の論点（次アクション）
- （なし）
