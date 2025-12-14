# diff_handler_failure_handling_20251214

## 背景
MVP の `ForEachAsync` を運用可能にするため、ハンドラ例外時の挙動（Retry/DLQ/Commit）を固定する。

## 決定事項
- `OnError=Retry` のときだけ Retry（fixed / 全例外 / 既定回数）を行う。
- retry が尽きた場合は warning を残し、例外として error を残す。
- 設定とガードが許す場合のみ、DLQ（handler_error）へ `DlqEnvelope` を送る（raw payload は持たない）。
- `autoCommit=false` の場合は、例外時に commit してスキップする。

## 参照
- `docs/contracts/handler_failures.md`

