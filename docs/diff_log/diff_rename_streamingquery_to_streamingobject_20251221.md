# diff: Rename `StreamingQueryAttribute` to `StreamingObjectAttribute`

## Summary
- Rename the attribute used to name engine-side objects from `StreamingQueryAttribute` to `StreamingObjectAttribute`.
- Rename the corresponding naming concept from `QueryName` to `ObjectName` in the Streaming spec.

## Motivation
- Provisioning creates engine artifacts (stream/table/view), not “queries” as standalone entities.
- The attribute name should describe what it controls: the engine object name used for idempotent registration (`CREATE IF NOT EXISTS`).

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
