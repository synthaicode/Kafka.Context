# diff: Add KsqlDsl:Streaming:Flink:Sources config section for WITH properties

## Summary
- Added `KsqlDslOptions.Streaming.Flink` configuration classes:
  - `KsqlDsl:Streaming:Flink:With` (global WITH properties)
  - `KsqlDsl:Streaming:Flink:Sources` (per-topic source WITH properties)
  - `KsqlDsl:Streaming:Flink:Sinks` (per-topic sink WITH properties)
- Added `FlinkKafkaConnectorOptionsFactory.From(KsqlDslOptions)` to map appsettings into `FlinkKafkaConnectorOptions`.
- Updated Flink DDL generation to merge `WITH` properties (per-topic overrides global; other duplicates fail-fast).

## Motivation
- Flink `WITH (...)` is table/topic scoped; we want a stable place in `appsettings.json` to manage per-topic connector properties without pushing that logic into code.

## Files
- Added: `src/Kafka.Context.Application/Configuration/StreamingOptions.cs`
- Updated: `src/Kafka.Context.Application/Configuration/KsqlDslOptions.cs`
- Updated: `src/Kafka.Context.Application/PublicAPI.Shipped.txt`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkKafkaConnectorOptions.cs`
- Added: `src/Kafka.Context/Streaming/Flink/FlinkKafkaConnectorOptionsFactory.cs`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkDialectProvider.cs`
- Updated: `src/Kafka.Context/PublicAPI.Shipped.txt`
- Updated: `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs`
- Updated: `examples/streaming-flink/Program.cs`
- Updated: `examples/streaming-flink-flow/Program.cs`
- Updated: `docs/wiki/streaming-api.md`
