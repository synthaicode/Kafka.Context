# diff_schema_scaffold_cli_20251220

## Summary
- Set Schema Registry global compatibility intention to `FULL` for schema-scaffold validation runs.

## Change
- `docs/environment/docker-compose.current.yml`: add `SCHEMA_REGISTRY_COMPATIBILITY_LEVEL=FULL` to the `schema-registry` service.

## Notes (local docker state)
- Schema Registry stores global compatibility in Kafka (`_schemas`) and may retain the previous value across restarts when Kafka data is persisted (this repo uses a named volume for Kafka).
- For an existing local environment, apply the setting explicitly via REST after restart:
  - `PUT http://127.0.0.1:18081/config` with body `{"compatibility":"FULL"}`

## Validation (performed)
- Restart: `docker compose -f docs/environment/docker-compose.current.yml up -d`
- Set config (if needed): `curl -X PUT -H "Content-Type: application/vnd.schemaregistry.v1+json" --data '{"compatibility":"FULL"}' http://127.0.0.1:18081/config`
- Physical tests:
  - `dotnet test tests/physical/Kafka.Context.PhysicalTests/Kafka.Context.PhysicalTests.csproj -c Release --filter "FullyQualifiedName~KsqlLinqSchemaRegistryStateTests|FullyQualifiedName~KsqlDbPocoConsumePhysicalTests"` (with `KAFKA_CONTEXT_PHYSICAL=1`)
- CLI verify (subject: `physical_cli_poco_out-value`):
  - `dotnet run --project src/Kafka.Context.Cli/Kafka.Context.Cli.csproj -c Release -- schema scaffold --sr-url http://127.0.0.1:18081 --subject physical_cli_poco_out-value --output artifacts/schema-scaffold-cli/Generated --force`
  - `dotnet run --project src/Kafka.Context.Cli/Kafka.Context.Cli.csproj -c Release -- schema verify --sr-url http://127.0.0.1:18081 --subject physical_cli_poco_out-value --fingerprint <from generated [SchemaFingerprint]>`

