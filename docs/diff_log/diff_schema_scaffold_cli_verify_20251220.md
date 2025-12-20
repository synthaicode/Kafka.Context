# diff: Schema scaffold plan (CLI/CI verify, runtime unchanged)

## Summary
- Revise the schema-scaffold requirements so that Kafka.Context runtime remains **POCO → Schema** as the source of truth.
- Move fingerprint-based fail-fast verification to **CLI/CI** (`schema verify`) instead of `ProvisionAsync()`.

## Motivation
- Avoid expanding runtime responsibility into “external SR schema is the source of truth”.
- Keep Kafka.Context aligned with existing contracts (small runtime surface; provisioning is register/verify).

## Decision
- Add an external CLI workflow:
  - `schema scaffold`: generate POCOs from SR as a starting point.
  - `schema verify`: compare SR latest schema vs POCO→Schema fingerprint (CI/desktop preflight).
- Runtime behavior stays unchanged:
  - `ProvisionAsync()` continues register/verify against Schema Registry.

## Document changes
- `docs/schema-scaffold-requirements.md` now:
  - states **POCO → Schema** is the truth for runtime
  - removes “extend ProvisionAsync to do fingerprint verification”
  - adds `schema verify` as the fail-fast mechanism
