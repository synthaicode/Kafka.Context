# diff: Add typed shortcuts for common Flink WITH keys

## Summary
- Added typed config shortcuts under `KsqlDsl:Streaming:Flink`:
  - `ScanStartupMode` → `scan.startup.mode`
  - `SourceGroupIdByTopic` → `properties.group.id` (per-topic sources)
- Updated `FlinkKafkaConnectorOptionsFactory` to merge these shortcuts into `FlinkKafkaConnectorOptions`.
- Updated docs to show the recommended usage.

## Motivation
- Avoid typos in the most frequently used Flink Kafka connector keys while keeping the rest flexible via dictionaries.

## Files
- Updated: `src/Kafka.Context.Application/Configuration/StreamingOptions.cs`
- Updated: `src/Kafka.Context.Application/PublicAPI.Shipped.txt`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkKafkaConnectorOptionsFactory.cs`
- Updated: `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs`
- Updated: `docs/wiki/streaming-api.md`
