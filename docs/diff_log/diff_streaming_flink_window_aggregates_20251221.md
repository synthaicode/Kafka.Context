# diff: streaming flink window aggregates (2025-12-21)

## Summary

- Flink の Window（TUMBLE/HOP）で使う集計関数（COUNT/SUM）を追加し、CTAS 判定と SQL 生成を通した。

## Added

- `Kafka.Context.Abstractions`:
  - `Kafka.Context.Streaming.Flink.FlinkAgg`（`Count()` / `Sum(...)`）

## Implemented

- `KafkaContext`:
  - `FlinkAgg` の使用を検出して `HasAggregate` を補完（CTAS 判定に反映）
- `FlinkSqlRenderer`:
  - `FlinkAgg.Count()` → `COUNT(*)`
  - `FlinkAgg.Sum(x)` → `SUM(x)`

## Examples / Tests

- examples:
  - `examples/streaming-flink/Program.cs` の tumble/hop サンプルに `Cnt/TotalAmount` を追加
- unit tests:
  - `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs` で `COUNT(*)` / `SUM(...)` の生成を固定

