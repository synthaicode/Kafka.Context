# diff: SR-driven consume via DynamicTopicSet

## Summary
- Add `KafkaContext.Topic(string topic)` to consume arbitrary topics by name.
- Add `DynamicTopicSet.ForEachAsync((key, value, headers, meta) => ...)` for bridge-style consumption where Schema Registry is the source of truth.

## Key contract
- If the key looks like Confluent Avro payload, the handler receives `key` as `GenericRecord`.
  - This covers windowed keys (tumbling/hopping) where window boundaries are encoded in the key record.
- Otherwise, the handler receives `key` as `byte[]` (SR-unregistered primitive/unknown encoding).

## Value contract
- `value` is decoded as Confluent Avro payload and passed as `GenericRecord`.

## Impact
- Public API addition (new consume entry point).
- No changes to existing `EventSet<T>` behavior.

