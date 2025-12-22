# Kafka.Context

Kafka + Schema Registry, with a small, opinionated, context-centric API (Avro contracts).

NuGet: `Kafka.Context`  
Target frameworks: .NET 8 / .NET 10  
Source: https://github.com/synthaicode/Kafka.Context

## Packages
- `Kafka.Context` - Core runtime (produce/consume, Schema Registry, DLQ)
- `Kafka.Context.Streaming` - Engine-agnostic streaming DSL
- `Kafka.Context.Streaming.Flink` - Flink SQL dialect + provisioning
- `Kafka.Context.Cli` - Schema scaffold/verify + AI guide export

## What it is
- A minimal runtime for producing/consuming Kafka messages with Schema Registry-backed Avro schemas
- A “KafkaContext” mental model (EF-like: `KafkaContext` ~= `DbContext`, `EventSet<T>` ~= `DbSet<T>`)
- Explicit failure handling (Retry / DLQ)
- A streaming DSL (separate packages) with Flink SQL support

## Non-goals
- No embedded stream processing engine (Kafka Streams / Flink runtime not bundled)
- No ksqlDB dialect support (Flink-only streaming for now)
- No generic SQL/OLAP query generator beyond the streaming DSL
- No general-purpose Kafka client wrapper (this stays intentionally small)
- No runtime “Schema Registry schema → POCO” mapping layer (use the CLI scaffold/verify workflow)

## Docs
- Repository map: `overview.md`
- Contracts (Avro / DLQ / provisioning): `docs/contracts/`
- appsettings.json guide (EN): `docs/contracts/appsettings.en.md`
- Target usage shape: `docs/samples/target_code_shape.md`
- Release workflow: `docs/workflows/release_roles_and_steps.md`
- NuGet README (the package page text): `src/Kafka.Context/README.md`
- Streaming README: `src/Kafka.Context.Streaming/README.md`
- Flink streaming README: `src/Kafka.Context.Streaming.Flink/README.md`
- CLI README: `src/Kafka.Context.Cli/README.md`
- Streaming API notes (Flink): `docs/wiki/streaming-api.md`
- Flink examples: `examples/streaming-flink/` and `examples/streaming-flink-flow/`
- Schema scaffold/verify CLI (dotnet tool): https://www.nuget.org/packages/Kafka.Context.Cli

## Tests
- Unit: `dotnet test tests/unit/Kafka.Context.Tests/Kafka.Context.Tests.csproj -c Release`
- Physical (Windows + Docker): `tests/physical/Kafka.Context.PhysicalTests` (see `docs/workflows/release_roles_and_steps.md`)

## AI Assist
If you're unsure how to use this package, run `kafka-context ai guide --copy` and paste the output into your AI assistant.
