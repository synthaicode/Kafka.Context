# diff: Add CLI command to preview Flink WITH properties

## Summary
- Added `kafka-context streaming flink with-preview` command.
- The command reads `appsettings.json` and prints merged Flink Kafka connector `WITH (...)` properties for configured source/sink topics.
- Supports filters (`--kind`, `--topic`) and JSON output (`--json`).

## Motivation
- Make configuration review/validation possible without running `ProvisionStreamingAsync`.
- Reduce operational mistakes by showing the effective `WITH` map per topic.

## Files
- Updated: `src/Kafka.Context.Cli/Program.cs`
- Updated: `src/Kafka.Context.Cli/README.md`
