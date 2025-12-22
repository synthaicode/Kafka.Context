# diff: Validate scan.startup.mode and add SASL/SSL templates

## Summary
- Added fail-fast validation for Flink Kafka connector `scan.startup.mode` (sources only).
  - Allowed: `earliest-offset`, `latest-offset`, `group-offsets`, `timestamp`, `specific-offsets`
  - `timestamp` requires `scan.startup.timestamp-millis`
  - `specific-offsets` requires `scan.startup.specific-offsets`
- Added SASL/SSL configuration templates (as `properties.*`) to the English streaming wiki page.
- Updated CLI `with-preview` to validate the same constraints when previewing.

## Motivation
- Prevent production misconfiguration and make security setup copy/paste friendly.

## Files
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkDialectProvider.cs`
- Updated: `src/Kafka.Context.Cli/Program.cs`
- Updated: `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs`
- Updated: `docs/wiki/streaming-api.md`
