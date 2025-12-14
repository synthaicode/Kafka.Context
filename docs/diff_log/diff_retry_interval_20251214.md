# diff_retry_interval_20251214

## 背景
Retry の既定挙動を利用者にとって予測可能にするため、`retryInterval` の既定値を確定する。

## 決定事項
- `WithRetry(maxRetries, retryInterval)` の `retryInterval` 既定値は 1s とする（fixed backoff）。

## 未解決の論点（次アクション）
- `ErrorHandlingPolicy.ExponentialBackoff(...)` を正式仕様として整合させるか（表面 API と実行パスの整合）。

## Update（後続diffで解決）
- MVP は fixed のみ（exponential 非対応）: `docs/diff_log/diff_retry_exponential_not_supported_20251214.md`
