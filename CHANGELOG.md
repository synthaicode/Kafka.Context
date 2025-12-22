# Changelog

All notable changes to this project will be documented in this file.

This project follows Semantic Versioning (SemVer).

## Unreleased

- [BREAKING] Remove entity access modes (readOnly/writeOnly): `IModelBuilder.Entity<T>` no longer accepts flags and `EventSet<T>` no longer enforces read/write constraints.
- Streaming (spec): define query registry as context-instance scoped (no static global registry).
- Streaming (Flink): window/join constraints, output/sink modes, and DDL generation for sources/sinks/queries.
- CLI: `streaming flink with-preview` now outputs DDL and can include columns via `--assembly`.
- Schema Registry: auto-register schemas forced off to keep SR contracts explicit.

## 1.1.0 - 2025-12-20

- Initial public release work-in-progress: provisioning (Kafka + Schema Registry), Avro contracts, DLQ + retry, physical smoke tests, per-topic consumer/producer config, and NuGet packaging docs.
- Add `Kafka.Context.Cli` (dotnet tool) for Schema Registry workflows:
  - `schema subjects`: list/filter subjects
  - `schema scaffold`: generate POCO from SR subject (includes topic/subject/fingerprint attributes)
  - `schema verify`: fail-fast fingerprint check (CI-friendly; supports `--type` or `--fingerprint`)
- Add schema metadata attributes for generated types: `SchemaSubjectAttribute`, `SchemaFingerprintAttribute`.
- Add schema-scaffold feature docs and workflow checklist (`docs/schema-scaffold-requirements.md`, `features/schema-scaffold-cli/`).
- Add physical tests covering ksqlDB->SR subject creation and consuming ksqlDB Avro output as POCO.
- Update local docker-compose environment for compatibility validation runs (Schema Registry compatibility intent and restart/verify workflow).

