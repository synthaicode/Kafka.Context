# Changelog

All notable changes to this project will be documented in this file.

This project follows Semantic Versioning (SemVer).

## Unreleased

- (TBD)

## 1.1.0 - 2025-12-20

- Initial public release work-in-progress: provisioning (Kafka + Schema Registry), Avro contracts, DLQ + retry, physical smoke tests, per-topic consumer/producer config, and NuGet packaging docs.
- Add `dotnet-kafka-context` (dotnet tool) for Schema Registry workflows:
  - `schema subjects`: list/filter subjects
  - `schema scaffold`: generate POCO from SR subject (includes topic/subject/fingerprint attributes)
  - `schema verify`: fail-fast fingerprint check (CI-friendly; supports `--type` or `--fingerprint`)
- Add schema metadata attributes for generated types: `SchemaSubjectAttribute`, `SchemaFingerprintAttribute`.
- Add schema-scaffold feature docs and workflow checklist (`docs/schema-scaffold-requirements.md`, `features/schema-scaffold-cli/`).
- Add physical tests covering ksqlDB->SR subject creation and consuming ksqlDB Avro output as POCO.
- Update local docker-compose environment for compatibility validation runs (Schema Registry compatibility intent and restart/verify workflow).

