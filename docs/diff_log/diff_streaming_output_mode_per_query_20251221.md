# diff: Add per-query output mode (Changelog/Final)

## Summary
- Added `StreamingOutputMode` to select output semantics per `ToQuery` declaration.
- Default is `Changelog`; callers can opt into `Final` at the query level.
- Propagated `OutputMode` through `StreamingQueryRegistry` → `IStreamingDialectProvider.GenerateDdl(...)`.
- Added fail-fast constraints: `OutputMode.Final` requires a window query (TUMBLE/HOP); non-window queries must use `Changelog`.

## Motivation
- ksqlDB exposes `EMIT CHANGES` / `EMIT FINAL`; we need an explicit, per-query switch without introducing a global setting.
- Keep `OnModelCreating` declarations minimal and predictable while letting each dialect map the mode to its own capabilities.

## Files
- Added: `src/Kafka.Context.Abstractions/Streaming/StreamingOutputMode.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/StreamingQueryDefinition.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/IStreamingQueryRegistry.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/IStreamingDialectProvider.cs`
- Updated: `src/Kafka.Context.Abstractions/Streaming/EntityModelBuilderStreamingExtensions.cs`
- Updated: `src/Kafka.Context/Streaming/StreamingQueryRegistry.cs`
- Updated: `src/Kafka.Context/KafkaContext.cs`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkDialectProvider.cs`
- Updated: `src/Kafka.Context.Abstractions/PublicAPI.Shipped.txt`
- Updated: `src/Kafka.Context/PublicAPI.Shipped.txt`
