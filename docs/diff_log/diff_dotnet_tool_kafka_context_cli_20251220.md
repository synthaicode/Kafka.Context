# diff: Add dotnet tool CLI (kafka-context) + schema verify step

## Summary
- Add `dotnet-kafka-context` as a dotnet tool (`kafka-context` command).
- Provide `schema scaffold` (SR → POCO) and `schema verify` (SR ↔ fingerprint) entry points.
- Update release workflows (GitHub Actions + release procedure doc) to include packaging and schema verify guidance.

## Motivation
- Keep Kafka.Context runtime responsibility unchanged (POCO → Schema provisioning).
- Enable CI/desktop fail-fast checks against Schema Registry without adding runtime verification responsibilities.

## User-facing usage
- Install: `dotnet tool install -g dotnet-kafka-context`
- Scaffold: `kafka-context schema scaffold --sr-url <url> --subject <topic>-value --output ./Generated`
- Verify: `kafka-context schema verify --sr-url <url> --subject <topic>-value --type "<Type>, <Assembly>"`

## Notes / limitations (current)
- `schema scaffold` targets Avro schemas where the root is `record`.
- Fingerprint calculation is performed by the CLI (normalized JSON + SHA-256) to keep `scaffold` and `verify` consistent.

