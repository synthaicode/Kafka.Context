# diff: Add SinkMode (append-only / upsert) hint for window outputs

## Summary
- Added `StreamingSinkMode` (`AppendOnly` / `Upsert`) as a per-query option in `ToQuery`.
- Stored the selection in `StreamingQueryDefinition` and propagated it into `StreamingQueryPlan.SinkMode`.
- Added fail-fast constraints:
  - `SinkMode.Upsert` requires a window query (TUMBLE/HOP).
  - `SinkMode.Upsert` is only allowed for CTAS-kind (TABLE) queries.
- Flink dialect currently emits an informational comment (`-- sink: upsert (infra-dependent)`) when `Upsert` is selected.

## Motivation
- For Flink window aggregations, whether consumers observe intermediate updates or “final-like” behavior is primarily determined by sink/topic strategy (e.g., upsert sink + compaction), not by `ForEachAsync`.
- Keep `ForEachAsync` simple; push sink semantics selection to `ToQuery` declarations.

## Notes
- Flink SQL does not provide `EMIT CHANGES/FINAL` clauses like ksqlDB; `OutputMode` is treated as intent + constraints, while `SinkMode` expresses the expected sink/topic behavior.

## Files
- Added: `src/Kafka.Context.Abstractions/Streaming/StreamingSinkMode.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/StreamingQueryDefinition.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/IStreamingQueryRegistry.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/EntityModelBuilderStreamingExtensions.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/StreamingQueryPlan.cs`
- Updated: `src/Kafka.Context/Streaming/StreamingQueryRegistry.cs`
- Updated: `src/Kafka.Context/KafkaContext.cs`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkDialectProvider.cs`
- Updated: `docs/kafka-context-streaming-spec.md`
- Updated: `src/Kafka.Context.Abstractions/PublicAPI.Shipped.txt`
