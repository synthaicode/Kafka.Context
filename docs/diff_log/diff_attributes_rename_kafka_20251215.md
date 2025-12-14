# diff: Rename attributes from `Ksql*` to `Kafka*`

## Summary
- Add `KafkaTopicAttribute` / `KafkaKeyAttribute` / `KafkaTimestampAttribute` / `KafkaDecimalAttribute`.
- Keep `Ksql*` attribute names as backward-compatible aliases (deprecated).

## Rationale
- The runtime is Kafka-centric; `Ksql*` naming is misleading for users.
- `Kafka*` keeps the mental model consistent with `KafkaContext`.

## Impact
- Public API adds new attribute types; existing `Ksql*` continue to work.
- Examples/tests/docs move to `Kafka*` naming.

