# Merge Streaming packages

## Summary
- Removed `Kafka.Context.Streaming.Abstractions` and merged its API into `Kafka.Context.Streaming`.
- Removed placeholder `Kafka.Context.Streaming.KsqlDb`.

## Details
- `Kafka.Context.Streaming` now contains both Streaming API and implementation.
- Updated solution and workflows to pack only `Kafka.Context.Streaming` and `Kafka.Context.Streaming.Flink`.

## Files
- Removed:
  - `src/Kafka.Context.Streaming.Abstractions/`
  - `src/Kafka.Context.Streaming.KsqlDb/`
- Updated:
  - `Kafka.Context.sln`
  - `.github/workflows/publish-preview.yml`
  - `.github/workflows/nuget-publish.yml`
  - `src/Kafka.Context.Streaming/Kafka.Context.Streaming.csproj`
  - `src/Kafka.Context.Streaming.Flink/Kafka.Context.Streaming.Flink.csproj`
  - `src/Kafka.Context/Kafka.Context.csproj`
