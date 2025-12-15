# diff: AddAsync supports Kafka headers

## Summary
- Add `EventSet<T>.AddAsync(..., headers, ...)` overload to produce with Kafka headers.

## Rationale
- `ForEachAsync` already exposes `headers` to the handler; produce should be able to set them as well.

## Impact
- Public API addition (new overload).
- No behavior changes for existing `AddAsync` calls.

