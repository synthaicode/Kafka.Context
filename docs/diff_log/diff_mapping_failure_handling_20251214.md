# diff_mapping_failure_handling_20251214

## 背景
MVP の `ForEachAsync` を運用可能にするため、デシリアライズ/マッピング失敗時の取り回しを固定する。

## 決定事項
- マッピング失敗は（設定次第で）DLQ に送信し、その後は commit してスキップする。
- DLQ 送信は `EnableForDeserializationError` とガード（rate limit 等）を満たす場合のみ行う。

## 参照
- `docs/contracts/mapping_failures.md`

