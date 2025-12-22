# diff: Flink dialect generates source/sink catalog DDL

## Summary
- Added a source configuration registry (`IStreamingSourceRegistry`) to store per-entity Flink source settings (event-time + watermark).
- Added Flink model-builder extension `Entity<T>().FlinkSource(...)` to declare event-time configuration in `OnModelCreating`.
- Added catalog DDL generation surface (`IStreamingCatalogDdlProvider`) and implemented it in `FlinkDialectProvider`:
  - Generates `CREATE TABLE IF NOT EXISTS ... WITH ('connector'='kafka', ...)` for input topics (sources).
  - Generates `CREATE TABLE IF NOT EXISTS ... WITH ('connector'='kafka', ...)` for derived output topics (sinks).
- Updated `KafkaContext.ProvisionStreamingAsync` to execute source/sink DDLs (when supported by the dialect provider) before executing query DDLs.
- Added fail-fast rule: window queries require Flink source event-time configuration for their input topic entity.

## Motivation
- Flink windowing depends on time attributes and watermarks, which belong to the source table definition.
- Keep Flink-specific table/connector generation in the Flink namespace while keeping the common layer focused on query planning.

## Files
- Added: `src/Kafka.Context.Abstractions/Streaming/IStreamingCatalogDdlProvider.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/IStreamingSourceRegistry.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/IStreamingSourceRegistryProvider.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/StreamingEventTimeConfig.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/StreamingSourceConfig.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/StreamingSourceDefinition.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/StreamingSinkDefinition.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/StreamingTimestampSource.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkModelBuilderExtensions.cs`
- Added: `src/Kafka.Context.Abstractions/Streaming/Flink/FlinkSourceBuilder.cs`
- Added: `src/Kafka.Context/Streaming/StreamingSourceRegistry.cs`
- Added: `src/Kafka.Context/Streaming/Flink/FlinkKafkaConnectorOptions.cs`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkDialectProvider.cs`
- Updated: `src/Kafka.Context/KafkaContext.cs`
- Updated: `examples/streaming-flink/Program.cs`
- Updated: `examples/streaming-flink-flow/Program.cs`
- Updated: `docs/wiki/streaming-api.md`
- Updated: `src/Kafka.Context.Abstractions/PublicAPI.Shipped.txt`
- Updated: `src/Kafka.Context/PublicAPI.Shipped.txt`
