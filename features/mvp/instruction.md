# MVP: Context / Provisioning / DLQ

## 目的
Kafka + Schema Registry を「Context」で束ね、最小の I/O API（`AddAsync` / `ForEachAsync` / DLQ）を提供する。

## 実装計画
- `docs/implementation_plan.md`

## スコープ（MVP）
- Context 作成（Cluster + Schema Registry を束ねる）
- NuGet/namespace: `Kafka.Context`（`docs/contracts/naming.md`）
- 契約形式: Avro
- Subject 命名: `{topic}-key` / `{topic}-value`
- 互換性: SR 側の既定/subject 設定に従う（Context は設定しない。非互換は登録時に失敗→起動しない）
- 公開 API: `docs/contracts/public_api.md`
- Provisioning
  - Topic の作成/存在確認
  - Schema Registry の登録/存在確認
  - 互換性・命名規約の確定（失敗時は起動しない）
  - topic メタデータ入力: `KsqlTopicAttribute`（`docs/contracts/topic_attributes.md`）
  - Kafka topic provisioning: `docs/contracts/provisioning_kafka.md`
- I/O API
  - `AddAsync(key, poco)`
  - `ForEachAsync(handler)`
  - 失敗時に Retry → 収束しない場合 DLQ
  - `Dlq.ForEachAsync(handler)`
  - DLQ 形式: `DlqEnvelope`（`docs/contracts/dlq.md`）
    - raw payload は保持しない（元トピックのメタ情報のみ）
  - Retry 既定: 3（`OnError=Retry` のときだけ有効。`docs/contracts/retry.md`）
    - backoff は fixed のみ（exponential は MVP ではサポートしない）
  - DLQ topic: 1 context = 1 topic（既定 `dead_letter_queue`。`docs/contracts/dlq_topic.md`）
  - fail fast 境界: `docs/contracts/provisioning_failfast.md`
  - mapping 失敗処理: DLQ（設定次第）→ commit → skip（`docs/contracts/mapping_failures.md`）
  - handler 例外処理: Retry（設定次第）→ DLQ（設定次第）→（autoCommit=false のとき commit）→ skip（`docs/contracts/handler_failures.md`）
  - 観測（標準メトリクス/ログ）: `docs/contracts/observability.md`

## 受入条件（MVP）
- 起動前に Provisioning が完了し、失敗時は例外で停止する。
- `AddAsync` で書いたメッセージを `ForEachAsync` が処理できる。
- handler が失敗した場合、規定回数 Retry した後に DLQ に送れる。
- DLQ を `Dlq.ForEachAsync` で読める。

## 設計メモ（叩き台）
- 公開 API は「I/O と失敗の扱い」だけに寄せる（設定は Options で閉じる）。
- 例外は「利用者が次の行動を決められる粒度」に畳む（Provisioning/契約違反/実行時）。
  - 契約違反: Schema Registry の subject/互換性/型不一致
