# diff: Java (JVM) producer → C# DynamicTopicSet (physical)

## Summary
- Add a physical test that produces Avro+Schema Registry messages from the JVM side and consumes them in C# via `KafkaContext.Topic(...).ForEachAsync(...)`.

## Why
- Kafka/Schema Registry ecosystems are JVM-first (Java/Flink/ksqlDB). This test validates that C# consumption is compatible with JVM-produced Avro payloads and logical types.

## SR conventions (explicit)
- Subject naming strategy is **TopicNameStrategy** (`{topic}-value`).
- Producer uses Confluent `KafkaAvroSerializer` with `TopicNameStrategy` explicitly set for both key/value subjects.

## What the test does
- `tests/physical/Kafka.Context.PhysicalTests/JavaInteropProducerTests.cs`
  - Runs a small Java producer inside the existing `schema-registry` docker container (OpenJDK already available in Confluent images).
  - The Java producer uses Confluent `KafkaAvroSerializer` and registers the schema to Schema Registry.
  - The test prints the SR `subject/version/id` (via SR REST) after producing.
  - C# consumes from the topic using `DynamicTopicSet` and asserts values/types for:
    - `boolean`, `int`, `long`, `string`
    - `timestamp-millis` → may appear as `DateTime` or `long` in `GenericRecord` (helper normalizes to epoch millis)
    - `decimal` logical type → may appear as `AvroDecimal` or `byte[]` in `GenericRecord` (helper normalizes to `decimal`)
    - `map<string,string>` → may appear as `Dictionary` or `object[]` representation (helper normalizes to `Dictionary<string,string>`)

## Notes
- This avoids installing a JDK on the host machine; the docker stack already includes Java runtime/tooling.
- For an explicit compatibility + evolution scenario, see `docs/diff_log/diff_physical_schema_evolution_20251215.md`.
