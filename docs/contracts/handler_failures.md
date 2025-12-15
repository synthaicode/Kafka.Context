# Contract: Handler failure handling (Kafka.Context)

## 達成できる結果
ハンドラ例外が発生しても、Retry/DLQ/Commit の挙動が一貫し、運用で復旧しやすくなる。

## 標準挙動（決定）

### Retry
- `OnError=Retry` のときだけ Retry を有効にする。
- backoff は fixed、retry 対象は全例外。
- すべての retry が尽きた場合は warning を残し、以降は「通常のハンドラ失敗」と同じ扱いにする。

### Logging
- 1 レコード消費ごとに info（entity/topic/offset/timestamp）を残せる。
- 例外時は error（例外込み）を残す。

### DLQ（handler error）
- `DlqOptions.EnableForHandlerError=true` かつガード通過時のみ DLQ に送る。
- DLQ は raw payload を持たず、`DlqEnvelope`（meta + error）を送る。

### Commit
- `autoCommit=true`（既定）: 明示 commit はしない（consumer の auto-commit に任せる）。
- `autoCommit=false`: 例外時は（DLQ 送信の有無に関わらず）commit してスキップする。

## 擬似コード（移植元の形）
```csharp
var useRetry = _errorHandlingContext.ErrorAction == ErrorAction.Retry;
var policy = new RetryPolicy
{
    MaxAttempts = useRetry ? _errorHandlingContext.RetryCount + 1 : 1,
    InitialDelay = _errorHandlingContext.RetryInterval,
    Strategy = BackoffStrategy.Fixed,
    IsRetryable = _ => true
};
try
{
    await policy.ExecuteAsync(() => action(entity), linkedCts.Token, (attempt, _) =>
    {
        _errorHandlingContext.CurrentAttempt = attempt;
    }).ConfigureAwait(false);
}
catch (Exception ex)
{
    _errorHandlingContext.CurrentAttempt = policy.MaxAttempts;
    if (useRetry) { /* warn exhausted */ }
    /* log error */
    if (_dlqProducer != null && dlq.EnableForHandlerError && DlqGuard.ShouldSend(dlq, context.DlqLimiter, ex.GetType()))
    {
        var env = DlqEnvelopeFactory.From(meta, ex, ...);
        await _dlqProducer.ProduceAsync(env, linkedCts.Token).ConfigureAwait(false);
    }
    if (!autoCommit)
        _commitManager?.Commit(meta);
}
```

## 関連
- Retry: `docs/contracts/retry.md`
- DLQ: `docs/contracts/dlq.md`
- Mapping failure: `docs/contracts/mapping_failures.md`
