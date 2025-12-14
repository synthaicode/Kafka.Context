# diff: Rename attributes from `Ksql*` to `Kafka*`

## Summary
- Add `KafkaTopicAttribute` / `KafkaKeyAttribute` / `KafkaTimestampAttribute` / `KafkaDecimalAttribute`.
- Remove legacy `Ksql*` attribute names (rename to `Kafka*` before first release).

## Rationale
- The runtime is Kafka-centric; `Ksql*` naming is misleading for users.
- `Kafka*` keeps the mental model consistent with `KafkaContext`.

## Impact
- Public API uses `Kafka*` attribute names.
- Examples/tests/docs use `Kafka*` naming.
