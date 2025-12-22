# diff: Implement HAVING rendering for Flink (projection inlining)

## Summary
- Added HAVING validation in `KafkaContext.ProvisionStreamingAsync`:
  - HAVING requires `GroupBy`/aggregate.
  - HAVING requires an explicit `Select(...)` projection (MVP constraint).
- Updated Flink SQL renderer to inline projection members inside HAVING:
  - `Having(x => x.TotalAmount > 10)` becomes `HAVING (SUM(...) > 10)` when `TotalAmount` is defined by `Select(...)`.
- Added a unit test to lock the HAVING output shape.

## Motivation
- In Flink SQL, HAVING is evaluated on aggregated expressions; referring to select aliases is not reliably supported.
- Our DSL naturally expresses HAVING against projected members, so we inline the projection to keep SQL valid and predictable.

## Files
- Updated: `src/Kafka.Context/KafkaContext.cs`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkSqlRenderer.cs`
- Updated: `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs`
