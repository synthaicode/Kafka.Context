# diff: AddAsync headers usage in physical tests and examples

## Summary
- Physical tests verify `AddAsync(..., headers, ...)` → `ForEachAsync(..., headers, meta, ...)` header propagation.
- Quickstart example shows producing with headers and printing received headers/meta.
- DLQ envelope includes original record headers when a record is sent to DLQ.

## Rationale
- `ForEachAsync` already exposes headers/meta; produce should be able to set headers and validate the roundtrip.

## Impact
- Tests/examples only (no runtime change).
