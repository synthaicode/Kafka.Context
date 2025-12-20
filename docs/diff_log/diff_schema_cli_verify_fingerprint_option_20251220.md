# diff: CLI verify supports `--fingerprint` (no assembly loading)

## Summary
- Update `kafka-context schema verify` to accept `--fingerprint <hex>` as an alternative to `--type`.
- Remove the need for an explicit assembly-loading option for CI/discovery use cases.

## Motivation
- The CLI is primarily SR-oriented; CI often wants a pure “SR schema ↔ expected fingerprint” check.
- Avoid requiring the target application's build output to be loadable from the dotnet tool process.

## Behavior
- `schema verify` now requires either:
  - `--fingerprint <hex>` (preferred for CI), or
  - `--type <assembly-qualified-type>` (reads `[SchemaFingerprint]` from the type).
