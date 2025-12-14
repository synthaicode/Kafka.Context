# diff_retry_exponential_not_supported_20251214

## 背景
Retry の挙動は運用事故に直結するため、「表面 API と実装の不整合」を残さない方針を確定する。

## 決定事項
- MVP では ExponentialBackoff を正式仕様に含めない。
- Retry は fixed backoff のみを正式仕様とする。
  - `WithRetry(maxRetries, retryInterval)` の `retryInterval` 既定は 1s。

## 影響
- `ErrorHandlingPolicy.ExponentialBackoff(...)` 相当の API は Kafka.Context では公開しない（または将来追加時は diff_log を追加して合意する）。

