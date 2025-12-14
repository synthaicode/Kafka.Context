# diff_retry_default_interval_keep_1s_20251214

## 背景
Retry の既定間隔を「即時（0ms）」に寄せる案が出たため、MVP の既定値を再確認する。

## 決定事項
- Retry の既定 `retryInterval` は 1s（fixed）を維持する。
- 即時 retry が必要な場合は `WithRetry(..., TimeSpan.Zero)` を明示指定して利用する。

