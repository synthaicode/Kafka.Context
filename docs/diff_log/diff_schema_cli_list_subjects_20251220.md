# diff: CLI adds `schema subjects` (list Schema Registry subjects)

## Summary
- Add a discovery command to the dotnet tool: `kafka-context schema subjects`.

## Why
- `schema scaffold`/`schema verify` operate on **subjects**.
- In real environments, a topic may map to multiple subjects (naming strategies, legacy subjects, key/value separation), so listing subjects reduces trial-and-error and makes CI setup explicit.

## Behavior
- Lists subjects from Schema Registry (`GET /subjects` via Confluent client) and prints them sorted.
- Supports filters:
  - `--prefix <prefix>`
  - `--contains <text>`
  - `--json` for machine-readable output
