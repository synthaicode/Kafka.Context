# Contract: Mapping failure handling (Kafka.Context)

## 達成できる結果
デシリアライズ/マッピング失敗時に処理が詰まらず、DLQ に必要最小の情報を残して先へ進める。

## 標準挙動（決定）
- デシリアライズ/マッピング失敗は「スキップ」扱いとする。
- 設定が許可し、ガード（rate limit 等）を通る場合のみ DLQ に送る。
- DLQ 送信の有無に関わらず、対象レコードは commit して前進する。

## 前提（Scope）
- `ForEachAsync` は Avro record → POCO の “同名/同型” マッピングを前提とする。
- 外部システムの schema を POCO に変換する mapping/alias 層は Non-Goals（このドキュメントの「mapping failure」には、そのような不一致も含まれる）。

## 擬似コード（移植元の形）
```csharp
internal static async Task HandleMappingException<TKey, TValue>(
    ConsumeResult<TKey, TValue> result,
    Exception ex,
    IDlqProducer dlqProducer,
    IConsumer<TKey, TValue> consumer,
    DlqOptions options,
    IRateLimiter limiter,
    CancellationToken cancellationToken)
    where TKey : class where TValue : class
{
    if (options.EnableForDeserializationError && DlqGuard.ShouldSend(options, limiter, ex.GetType()))
    {
        var allowHeaders = ExtractAllowedHeaders(result.Message.Headers, options.HeaderAllowList, options.HeaderValueMaxLength);
        var env = DlqEnvelopeFactory.From(result, ex,
            options.ApplicationId, options.ConsumerGroup, options.Host, allowHeaders,
            options.ErrorMessageMaxLength, options.StackTraceMaxLength, options.NormalizeStackTraceWhitespace);
        await dlqProducer.ProduceAsync(env, cancellationToken).ConfigureAwait(false);
    }
    consumer.Commit(result);
}
```

## 関連
- DLQ envelope: `docs/contracts/dlq.md`
- Retry/commit: `docs/contracts/retry.md`
