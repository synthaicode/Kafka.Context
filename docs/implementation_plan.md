# Implementation plan (MVP)

この文書は Kafka.Context の MVP を「実装→検証→公開」まで進めるための実装計画を示す。

## ステータス

- MVP は **達成済み**（本ドキュメントは「達成した計画の記録」として残す）。
- Streaming の要件・設計は `docs/kafka-context-streaming-spec.md` と `docs/kafka-context-master-plan.md` を正とする。

## ゴール（MVP）
- Context 作成 → Provisioning（Kafka topic + Schema Registry） → `AddAsync` / `ForEachAsync`
- 失敗時: Retry（fixed/既定 1s/3回）→ DLQ（raw payload なし、`DlqEnvelope`）
- SR:
  - contract: Avro
  - subject: `{topic}-key` / `{topic}-value`
  - compatibility level は SR 側の既定に従い、非互換は登録時に失敗（fail fast）
- Provisioning（fail fast）:
  - topic 設定不一致は停止（完全一致）
  - schema fullname 不一致は停止
  - 監視フェーズの subject 待機は上限付き（appsettings）で、SR エラーは停止

## スコープ境界（Non-Goals）
- ksqlDB エンジン/クエリ生成（Window/JOIN/集約）
- Bus/Worker などの実行フレームワーク

> 注: 上記 Non-Goals は「MVP スコープ外」を指す。Streaming の検討/実装を否定するものではない。

## マイルストーン

### M0: ソリューション土台
- `src/` に .NET 8 / .NET 10 マルチターゲットのプロジェクトを配置
- `tests/unit` / `tests/physical` の土台
- `examples/` の土台

### M1: 公開 API（コンパイル可能）
- `KafkaContext` + `KafkaContextOptions`
- `EventSet<T>`
  - `AddAsync(...)`（最小）
  - `ForEachAsync(...)` overload 群（`docs/contracts/public_api.md`）
  - `Commit(MessageMeta meta)`（manual commit）
- attributes
  - `KafkaTopicAttribute`（topic metadata）
  - key/timestamp/decimal 等（必要最小）

### M2: 設定（appsettings）
- `KsqlDsl` セクション読込（`docs/contracts/appsettings.md`）
- topic 単位設定（`KsqlDsl.Topics.<topicName>.Creation.*`）
- SR monitoring 設定キー
  - `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitTimeout`
  - `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitMaxAttempts`

### M3: Provisioning（Kafka）
- AdminClient による create-if-missing
- 既存 topic の設定完全一致チェック（指定がある場合）
- DLQ topic 自動作成（推奨既定 config を適用）
  - `cleanup.policy=delete`
  - `retention.ms=5000`
  - 追加/上書きは `KsqlDsl.Topics.<dlqTopicName>.Creation.Configs`

### M4: Provisioning（Schema Registry / Avro）
- Avro SerDes（Confluent）で登録/検証
- subject 命名 `{topic}-key` / `{topic}-value`
- SR 互換性 NG は登録時に例外→ fail fast
- schema fullname 不一致は契約違反→ fail fast
- 監視フェーズ:
  - subject 待機（上限付き）
  - SR エラーは fail fast

### M5: Consume / Error handling / DLQ
- `ForEachAsync` consume loop
- mapping failure
  -（設定次第）DLQ → commit → skip
- handler failure
  - Retry（OnError=Retry のときだけ、fixed/1s/3回）
  -（設定次第）DLQ
  - autoCommit=false のとき record 単位 commit で前進
- DLQ
  - `DlqEnvelope`（raw payload なし、timestamp 取れない場合は空）
  - fingerprint（SHA-256 + 正規化オプション）
  - `KafkaContext.Dlq.ForEachAsync(...)` で読み取れる

### M6: 観測・サンプル・テスト
- 観測（構造化ログ + optional counters）
  - `docs/contracts/observability.md` のカウント規約を実装に反映
- examples
  - quickstart / manual-commit / error-handling-dlq
- tests/unit
  - subject 命名、retry 回数/間隔、DLQ envelope/fingerprint、設定バインド等
- tests/physical（Docker 前提）
  - Provisioning（topic 作成/一致チェック、SR 登録）
  - AddAsync→ForEachAsync→DLQ 経路

## 作業の進め方（運用）
- 変更は `features/mvp/instruction.md` を起点に進める。
- 外部仕様の変更は `docs/diff_log/` に差分を追加する。
- Docker 環境は `docs/environment/docker-compose.current.yml` を参照する。
