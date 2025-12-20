# diff: Physical tests for SR-driven DynamicTopicSet

## Summary
- Add physical tests that verify `KafkaContext.Topic(topic).ForEachAsync((key, value, headers, meta) => ...)` decoding rules.

## Covered cases
- Avro key + Avro value:
  - Key is produced as Confluent Avro payload and decoded as `GenericRecord`.
  - Validates key fields (including window-like boundaries).
- Raw key + Avro value:
  - Key is produced as non-Avro bytes and passed through as `byte[]`.

## Impact
- Physical tests only; no behavior change in runtime.

