# diff: Streaming windows become dialect-specific + ToQuery override by dialect

## Summary
- Remove Window APIs from the common Streaming surface; treat windows as dialect-specific APIs.
- Treat `options.UseStreaming<FlinkDialectProvider>()` as pseudo-code until the final registration point is fixed.
- Define `ToQuery(...)` as a declaration hook in the common layer, and generate actual queries in the dialect-specific override.

## Motivation
- Window semantics differ significantly between engines (event-time/processing-time, watermark, emit/final, within/grace), so a “common window API” would either be leaky or become a least-common-denominator.
- Keeping registration API flexible avoids committing to an integration shape before the runtime packages are implemented.
- Splitting `ToQuery` into “declaration vs generation” supports a single modeling experience (`OnModelCreating`) while delegating executable artifacts to the selected backend.

## User-visible behavior
- Common layer supports core relational operators (filter/project/group/join/limit/order) without windows.
- Window DSLs live under backend namespaces (e.g., `Kafka.Context.Streaming.Flink`, `Kafka.Context.Streaming.Ksql`).
