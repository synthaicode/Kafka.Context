# diff: Document Flink-compatible C# -> Avro mapping subset

## Summary
- Added a section to the English streaming wiki page describing the Flink-compatible subset of C# attributes used for Avro mapping.
- Explicitly documented which attributes are supported and which ksqlDB-specific ones are excluded.

## Motivation
- Keep SR registration and DDL generation aligned with the agreed “C# is source of truth” flow.

## Files
- Updated: `docs/wiki/streaming-api.md`
