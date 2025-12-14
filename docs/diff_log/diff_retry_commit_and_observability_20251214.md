# diff_retry_commit_and_observability_20251214

## 背景
MVP の `ForEachAsync` を運用可能にするため、commit 粒度と観測（retry/DLQ/skip/commit）を標準化する。

## 決定事項
- `autoCommit=false` の commit 粒度は record 単位（1 レコード処理ごと）を標準とする。
- `autoCommit=true` は Kafka の auto-commit に委譲し、本ライブラリは明示 commit を行わない。
- 観測の標準として、構造化ログ（ILogger）と、採用可能なら counter 群を定義する。
  - 仕様: `docs/contracts/observability.md`

