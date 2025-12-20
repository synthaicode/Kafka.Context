# feature: Schema scaffold CLI (dotnet-kafka-context)

## Goal
Validate the end-to-end workflow:
1) SR has schemas (optionally registered via Ksql.Linq / external systems)
2) `kafka-context` CLI can discover subjects, scaffold POCO, and verify fingerprint
3) The generated POCO can be integrated into an app using Kafka.Context and used to produce/consume
4) Compatibility changes can be validated by adjusting `docs/environment/docker-compose.current.yml`

Related docs:
- Requirements: `docs/schema-scaffold-requirements.md`
- CLI usage: `src/Kafka.Context.Cli/README.md`

---

## Prerequisites
- Docker Desktop (or equivalent)
- .NET SDK (net8.0+)
- Kafka/SR/ksqlDB stack: `docs/environment/docker-compose.current.yml`

Start environment:
```powershell
docker compose -f docs/environment/docker-compose.current.yml up -d
```

Build CLI locally:
```powershell
dotnet build src/Kafka.Context.Cli/Kafka.Context.Cli.csproj -c Release
```

---

## Test checklist (track progress here)

### A) CLI smoke (argument handling / exit codes)
- [x] `--help` prints usage: `dotnet run --project src/Kafka.Context.Cli/Kafka.Context.Cli.csproj -- --help`
- [x] `schema scaffold` missing `--subject` exits non-zero (2)
- [x] `schema verify` missing both `--type/--fingerprint` exits non-zero (2)
- [x] `schema subjects` missing `--sr-url` (and env not set) exits non-zero (2)

### B) SR discovery (`schema subjects`)
- [x] List all subjects: `kafka-context schema subjects --sr-url http://127.0.0.1:18081`
- [x] Filter by prefix: `kafka-context schema subjects --sr-url http://127.0.0.1:18081 --prefix <topic>-`
- [x] Filter by contains: `kafka-context schema subjects --sr-url http://127.0.0.1:18081 --contains -value`
- [x] JSON output is valid: `kafka-context schema subjects --sr-url http://127.0.0.1:18081 --json`

### C) Scaffold + Verify (SR → POCO → fingerprint check)
Prepare at least one target subject (example: `<topic>-value`).
- [x] Dry-run scaffold prints code: `kafka-context schema scaffold --sr-url http://127.0.0.1:18081 --subject <topic>-value --dry-run`
- [x] Generate file: `kafka-context schema scaffold --sr-url http://127.0.0.1:18081 --subject <topic>-value --output ./Generated --force`
- [x] Extract fingerprint from generated `[SchemaFingerprint("...")]`
- [x] Verify by fingerprint (CI-style): `kafka-context schema verify --sr-url http://127.0.0.1:18081 --subject <topic>-value --fingerprint <hex>`
- [x] Negative check: verify with a wrong fingerprint returns exit code 4

### D) Integration with Kafka.Context (produce/consume)
Goal: prove that a generated POCO can be used without changing the runtime workflow.
- [x] Add generated file into your app project (or a test harness project)
- [x] Run `ProvisionAsync()` (should succeed when schemas are compatible)
- [x] Produce at least 1 record (`AddAsync`) or have ksqlDB produce output
- [x] Consume it (`ForEachAsync`) and assert the expected shape
- [x] Physical smoke (baseline) passes: `dotnet test tests/physical/Kafka.Context.PhysicalTests/Kafka.Context.PhysicalTests.csproj -c Release --filter FullyQualifiedName~SmokeTests`
  - POCO consume proof: `dotnet test tests/physical/Kafka.Context.PhysicalTests/Kafka.Context.PhysicalTests.csproj -c Release --filter FullyQualifiedName~KsqlDbPocoConsumePhysicalTests`

### E) Compatibility validation via docker-compose
Goal: validate behavior when SR compatibility settings are changed.
- [x] Update `docs/environment/docker-compose.current.yml` to the intended SR compatibility setup
- [x] Restart the SR container(s): `docker compose -f docs/environment/docker-compose.current.yml up -d`
- [x] Re-run section C (verify) and confirm expected pass/fail
- [x] ksqlDB creates SR subjects via Ksql.Linq: `dotnet test tests/physical/Kafka.Context.PhysicalTests/Kafka.Context.PhysicalTests.csproj -c Release --filter FullyQualifiedName~KsqlLinqSchemaRegistryStateTests`

---

## Evidence (optional but recommended)
Record outputs you want to keep as evidence:
- CLI: subject list, scaffold output path, verify exit codes
- Kafka.Context: `ProvisionAsync` logs, produce/consume result

Suggested log (fill in):
- Date:
- Docker compose hash / file revision:
- Target subject(s):
- Result summary:

Latest (2025-12-20):
- Target subject: `physical_cli_poco_out-value`
- SR global compatibility: `FULL`
- CLI: `schema scaffold` + `schema verify` OK
