# diff: CLI with-preview emits columns from POCOs

## Summary
- Added `--assembly` support to `kafka-context streaming flink with-preview` so DDL previews can include column definitions by resolving `[KafkaTopic]` POCOs.
- Kept placeholder-column skeletons when no POCO is available (or when `--allow-missing-types` is used).

## Motivation
- Make `with-preview` closer to real Flink DDL by using the same type mapping as provisioning.

## Files
- Updated: `src/Kafka.Context.Cli/Program.cs`
- Updated: `src/Kafka.Context.Cli/README.md`
- Updated: `docs/wiki/streaming-api.md`
