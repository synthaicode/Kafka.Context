# Kafka.Context

Kafka + Schema Registry, with a small, opinionated, context-centric API (Avro contracts).

NuGet: `Kafka.Context`  
Target frameworks: .NET 8 / .NET 10  
Source: https://github.com/synthaicode/Kafka.Context

## What it is
- A minimal runtime for producing/consuming Kafka messages with Schema Registry-backed Avro schemas
- A “KafkaContext” mental model (EF-like: `KafkaContext` ~= `DbContext`, `EventSet<T>` ~= `DbSet<T>`)
- Explicit failure handling (Retry / DLQ)

## Non-goals
- No stream processing engine (Kafka Streams / Flink)
- No query generation or DSL (no JOIN / Window / Aggregation)
- No general-purpose Kafka client wrapper (this stays intentionally small)
- No “external Schema Registry schema → POCO” mapping layer (consume assumes the Avro contract matches your POCO)

## Docs
- Repository map: `overview.md`
- Contracts (Avro / DLQ / provisioning): `docs/contracts/`
- appsettings.json guide (EN): `docs/contracts/appsettings.en.md`
- Target usage shape: `docs/samples/target_code_shape.md`
- Release workflow: `docs/workflows/release_roles_and_steps.md`
- NuGet README (the package page text): `src/Kafka.Context/README.md`
- Schema scaffold/verify CLI (dotnet tool): `src/Kafka.Context.Cli/README.md`

## Tests
- Unit: `dotnet test tests/unit/Kafka.Context.Tests/Kafka.Context.Tests.csproj -c Release`
- Physical (Windows + Docker): `tests/physical/Kafka.Context.PhysicalTests` (see `docs/workflows/release_roles_and_steps.md`)
