# diff_retry_policy_20251214

## 背景
MVP の「失敗時 Retry / DLQ / 再処理」を実装するため、Retry の標準挙動を確定する。

## 決定事項
- Retry は `OnError=Retry` のときだけ有効とする。
- 回数既定値は 3 とする。
- backoff は fixed（一定間隔）とする。
- retry 対象は全例外（`IsRetryable = _ => true`）とする。
- commit の標準は `docs/contracts/retry.md` の通りとする。

