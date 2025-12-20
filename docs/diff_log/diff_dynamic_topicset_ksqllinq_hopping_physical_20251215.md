# diff: DynamicTopicSet hopping (Ksql.Linq-based physical test)

## Summary
- Add a ksqlDB-backed physical test that creates a hopping window aggregation via `Ksql.Linq` (v1.1.x) and validates that `KafkaContext.Topic(topic).ForEachAsync(...)` can consume the output using Schema Registry as the source of truth.

## Why
- The main “bridge” scenario is consuming topics produced by external systems (e.g., ksqlDB/Flink) where the Avro schema contract is already registered in Schema Registry.
- Hopping/Tumbling results are a common case where key serialization can differ (window metadata can be represented as Avro fields or as trailing bytes).

## Test coverage
- `tests/physical/Kafka.Context.PhysicalTests/DynamicTopicSetKsqlLinqTests.cs`
  - Creates ksqlDB objects with `KEY_FORMAT='AVRO'` and `VALUE_FORMAT='AVRO'`.
  - Produces input via `Ksql.Linq` (Avro + SR).
  - Consumes the output via `DynamicTopicSet` and asserts:
    - `value` is decoded as `GenericRecord`.
    - `key` is either `GenericRecord` (window bounds embedded as fields) or `WindowedKey` (window bytes appended); in both cases, the base key fields are available.

## Notes
- `Ksql.Linq` v1.1.x is used for hopping tests as requested.
- The test recreates the input topic and sets `auto.offset.reset=earliest` in ksqlDB to reduce race/flakiness.

