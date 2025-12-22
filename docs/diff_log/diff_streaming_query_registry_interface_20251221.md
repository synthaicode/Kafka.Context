# diff: Introduce `IStreamingQueryRegistry` (instance-scoped registry interface)

## Summary
- Add `IStreamingQueryRegistry` as the abstraction for storing `QueryDefinition` per `KafkaContext` instance.
- Update spec so `ToQuery` extension and `KafkaContext` hold the registry via `IStreamingQueryRegistry` (implementation default: `StreamingQueryRegistry`).

## Motivation
- Improve testability by allowing registry substitution/mocking.
- Allow future changes to storage (thread-safety, immutability, scoping) without changing consumer code.

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
