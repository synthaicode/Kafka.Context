# Streaming packages split (Streaming/Flink)

## Summary
- Split Streaming and Flink features into dedicated packages:
  - `Kafka.Context.Streaming.Abstractions`
  - `Kafka.Context.Streaming`
  - `Kafka.Context.Streaming.Flink`
  - `Kafka.Context.Streaming.KsqlDb` (placeholder)

## Details
- Moved core streaming API types from `Kafka.Context.Abstractions` to `Kafka.Context.Streaming.Abstractions`.
- Moved streaming query builder/extension/registry implementations to `Kafka.Context.Streaming`.
- Moved Flink-specific APIs and dialect implementation to `Kafka.Context.Streaming.Flink`.
- Added internals access for `Kafka.Context` and Flink implementation from `Kafka.Context.Streaming`.
- Updated examples/tests/CLI and workflows to reference new projects.

## Files
- Added:
  - `src/Kafka.Context.Streaming.Abstractions/`
  - `src/Kafka.Context.Streaming/`
  - `src/Kafka.Context.Streaming.Flink/`
  - `src/Kafka.Context.Streaming.KsqlDb/`
- Updated:
  - `Kafka.Context.sln`
  - `.github/workflows/publish-preview.yml`
  - `.github/workflows/nuget-publish.yml`
  - `examples/streaming-flink/FlinkStreamingExamples.csproj`
  - `examples/streaming-flink-flow/FlinkStreamingFlowExample.csproj`
  - `tests/unit/Kafka.Context.Tests/Kafka.Context.Tests.csproj`
  - `src/Kafka.Context.Cli/Kafka.Context.Cli.csproj`
  - `src/Kafka.Context/Kafka.Context.csproj`
