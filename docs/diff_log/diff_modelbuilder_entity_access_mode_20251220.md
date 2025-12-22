# diff: IModelBuilder returns builder + entity access modes (readOnly/writeOnly)

## Summary
- Change `Kafka.Context.Abstractions.IModelBuilder.Entity<T>(...)` to return `EntityModelBuilder<T>`.
- Add `readOnly` / `writeOnly` flags at modeling time and enforce them in `EventSet<T>`:
  - `readOnly`: disallow `AddAsync(...)`
  - `writeOnly`: disallow `ForEachAsync(...)`

## Motivation
- Support the `Entity<T>().ToQuery(...)` modeling style for Streaming without introducing a second root context.
- Make “derived/query result entities are readOnly” an enforceable rule (Fail-Fast at usage time).
- Allow future separation of input/output roles while keeping `KafkaContext` as the single entry point.

## Behavior
- Default (no explicit modeling): both read and write are allowed (backward-compatible).
- Invalid configuration (`readOnly` and `writeOnly` both true) throws immediately during model building.

## Notes
- `EntityModelBuilder<T>` currently carries access-mode metadata; `ToQuery(...)` will be introduced in the Streaming layer.
