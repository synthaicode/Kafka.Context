# diff_topic_consumer_settings_20251214

## 目的
別プロセス起動（producer/consumer 分離）でも前後関係に左右されずに読めるよう、topic 別に consumer 設定（GroupId / AutoOffsetReset 等）を appsettings で指定できるようにする。

## 変更概要
- `KsqlDsl.Topics.<topicName>.Consumer.*` を追加。
  - `GroupId`
  - `AutoOffsetReset`（`Earliest|Latest|Error`）
  - `AutoCommitIntervalMs`
  - `SessionTimeoutMs`
  - `HeartbeatIntervalMs`
  - `MaxPollIntervalMs`
  - `FetchMinBytes`
  - `FetchMaxBytes`
  - `IsolationLevel`（`ReadUncommitted|ReadCommitted`）
  - `AdditionalProperties`
- `KsqlDsl.Topics.<topicName>.Producer.*` を追加。
  - `Acks`（`All|Leader|None`）
  - `CompressionType`（`None|Gzip|Snappy|Lz4|Zstd`）
  - `EnableIdempotence`
  - `MaxInFlightRequestsPerConnection`
  - `LingerMs`
  - `BatchSize`
  - `BatchNumMessages`
  - `DeliveryTimeoutMs`
  - `RetryBackoffMs`
  - `AdditionalProperties`
- `KafkaConsumerService` は `Common.AdditionalProperties` → `Topics.<topic>.Consumer.*` の順で適用し、最後に `EnableAutoCommit` を引数で強制する。
- `KafkaProducerService` は `Common.AdditionalProperties` → `Topics.<topic>.Producer.*` の順で適用する。
- topic key の解決は `TopicExpectation` と同等に、`.pub/.int` の base topic と `_` prefix を遡って最長一致で検索する。

## 物理テスト
- クロスプロセス確認用 runner（`tests/physical/Kafka.Context.PhysicalRunner`）で `physical_orders_physical_proc` の consumer を `Earliest` に固定。
- 同 runner の producer で topic 単位の Producer 設定を付与（例: `Acks=All`, `EnableIdempotence=true`）。
