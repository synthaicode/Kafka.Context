# diff_handler_retry_then_dlq_20251214

## 目的
`EventSet<T>.ForEachAsync(...)` のハンドラ例外時に、DLQ 送付前の retry を可能にする。

## 背景
- これまで retry は `OnError(ErrorAction.Retry)` のときだけ有効で、`OnError(ErrorAction.DLQ)` とは併用できなかった。
- 物理テスト（および実運用想定）では「数回 retry → それでもダメなら DLQ」が欲しい。

## 変更概要
- `src/Kafka.Context/ErrorHandlingPolicy.cs` に `RetryEnabled` を追加（デフォルト `false`）。
- `src/Kafka.Context/EventSet.cs` で以下を変更:
  - `WithRetry(...)` が `RetryEnabled=true` をセットする。
  - `OnError(ErrorAction.Retry)` でも `RetryEnabled=true` をセットする（従来通り retry を有効化）。
  - ハンドラ実行は `RetryEnabled && RetryCount>0` の場合に retry し、最終的に失敗したら `ErrorAction` に従って処理する（DLQ の場合は DLQ へ）。

## 物理テスト側
- `tests/physical/Kafka.Context.PhysicalTests/SmokeTests.cs` の DLQ ケースに `.WithRetry(2, 200ms)` を追加し、`boom` を数回 retry してから DLQ に入る流れを確認できるようにした。

