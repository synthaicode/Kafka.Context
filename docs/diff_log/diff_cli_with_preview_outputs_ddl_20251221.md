# diff: CLI with-preview outputs DDL skeletons

## Summary
- Changed `kafka-context streaming flink with-preview` default output to `CREATE TABLE IF NOT EXISTS ... WITH (...)` DDL skeletons.
- JSON output (`--json`) remains available for tooling.

## Motivation
- Make it easier to review/apply the effective `WITH` configuration in a Flink-first workflow.

## Files
- Updated: `src/Kafka.Context.Cli/Program.cs`
- Updated: `src/Kafka.Context.Cli/README.md`
