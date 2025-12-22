# Kafka.Context / overview

このリポジトリは「Kafka + Schema Registry を、DB屋の感覚で扱えるようにする」ための **Context中心・契約駆動** の軽量ランタイムを作る。

## 何を提供するか（MVP）
- Provisioning（起動前に破綻を防ぐ）
  - Topic 作成/検証
    - 入力: `KsqlTopicAttribute` + `KsqlDsl.Topics.<topic>.Creation.*`（appsettings 優先）
  - Schema Registry 登録/検証
  - 互換性・命名規約の確定（失敗時は起動しない）
- IO API（最小）
  - 書き込み: `AddAsync(key, poco)`
  - 読み取り: `ForEachAsync(handler)`
  - 失敗時: Retry / DLQ / 再処理（運用で壊れやすい部分を Context に閉じる）
  - DLQ の読み取り: `Dlq.ForEachAsync(handler)`

## 契約形式（決定）
- Avro（Schema Registry 前提）

## DLQ（決定）
- DLQ value は `DlqEnvelope`（`docs/contracts/dlq.md`）
- DLQ topic 運用: `docs/contracts/dlq_topic.md`

## Retry（決定）
- 既定: 3（`OnError=Retry` のときだけ有効）/ fixed backoff / 全例外（`docs/contracts/retry.md`）

## Provisioning fail fast（決定）
- 互換性 NG は登録時に失敗して起動しない。subject 不在は登録で作成（`docs/contracts/provisioning_failfast.md`）

## Provisioning（Kafka）
- Kafka topic create-if-missing（AdminClient）: `docs/contracts/provisioning_kafka.md`

## Non-Goals（明示）
- 実行フレームワーク（Bus / Worker / Throttle 等）
- KafkaFlow の代替（否定ではなく別の上位抽象）

## フォルダ早見表
- `src/` NuGet のソース（`Kafka.Context` / `Kafka.Context.Streaming*`）
- `tests/` テスト（unit/physical）
- `examples/` 利用サンプル
- `docs/`
  - `docs/progress_management.md` 進捗ログ運用
  - `docs/contracts/` 契約（Avro 等）
  - `docs/samples/` 目標の利用例（コードの形）
  - `docs/environment/` 開発・検証環境（docker 等）
  - `docs/workflows/` 進め方（feature→実装→検証→公開）
  - `docs/diff_log/` 設計差分の履歴
- `features/`
  - `features/{機能名}/instruction.md` 作業の起点（目的・仕様・受入条件）
