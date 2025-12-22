# diff: Flink SQL Golden DDL UT + Flink-specific LINQ API

## Summary
- Added Flink-specific LINQ surface (`FlinkSql`, `FlinkSqlType`, intervals, time units, session window) to represent all `features/flink-sql-research/sql` cases.
- Extended Flink SQL renderer to handle the new functions and constraints (including explicit fail-fast for unsupported probes).
- Added L4 Golden tests that snapshot generated DDL for all supported cases; unsupported probes assert `NotSupportedException`.

## Motivation
- Fix Flink SQL compatibility via stable, query-level golden snapshots and enforce fail-fast for unsupported dialect shapes.

## Files
- Added: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkSql.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkSqlType.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkSqlInterval.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkTimeUnit.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkAgg.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkWindow.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkWindowExtensions.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/StreamingWindowKind.cs`
- Updated: `src/Kafka.Context.Abstractions/PublicAPI.Unshipped.txt`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkSqlRenderer.cs`
- Added: `tests/unit/Kafka.Context.Tests/FlinkSqlGoldenTests.cs`
- Added: `tests/unit/Kafka.Context.Tests/Golden/FlinkSql/*.ddl`
