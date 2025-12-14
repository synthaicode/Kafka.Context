# Contract: Observability (Retry / DLQ / Skip / Commit)

## 達成できる結果
障害時に「どれくらい失敗しているか」「Retry と DLQ が動いているか」「どれくらい skip しているか」を最低限追える。

## 方針（MVP）
- 観測は **構造化ログ（ILogger）を標準** とする。
- 追加で、実装側が採用する場合は **メトリクス（counter）** と **イベント（任意）** を提供できる余地を残す。

## 標準ログ（推奨）
- consumed（info）: entity / topic / offset / timestamp
- retry exhausted（warning）: entity / topic / maxAttempts / lastErrorType
- handler failed（error）: entity / topic / errorType / message（例外を含める）
- mapping failed（warning or error）: topic / errorType（例外を含める）
- dlq enqueued（info or warning）: phase（`mapping_error` / `handler_error`）/ dlqTopic / entity

## 標準メトリクス（決定）
以下の counter を標準とする（実装で採用する場合）。名前は snake/kebab ではなく **dot 区切り** に統一する。

- `kafka_context.consume_total`
- `kafka_context.handler_success_total`
- `kafka_context.handler_error_total`
- `kafka_context.mapping_error_total`
- `kafka_context.retry_attempt_total`
- `kafka_context.retry_exhausted_total`
- `kafka_context.dlq_enqueue_total`
- `kafka_context.skipped_total`
- `kafka_context.manual_commit_total`（`autoCommit=false` の commit）

### 推奨タグ（dimensions）
- `entity`（例: `Order`）
- `topic`（例: `orders`）
- `phase`（`mapping_error` / `handler_error` / `retry`）
- `auto_commit`（`true` / `false`）

## カウント規約（決定）
- `retry_attempt_total`: 実際に retry を試行した回数（初回実行は含めない）。
- `retry_exhausted_total`: retry を使った上で、最終的に例外へ到達した回数。
- `dlq_enqueue_total`: DLQ に投入した回数（guard で抑止されたものは含めない）。
- `skipped_total`:
  - mapping 失敗: commit して前進した回数
  - handler 失敗: 例外を捕捉してループを継続した回数（`autoCommit` に関わらず「そのレコードを通常処理しない」扱い）
- `manual_commit_total`: `autoCommit=false` で commit を実行した回数（成功・失敗スキップ双方を含む）。

