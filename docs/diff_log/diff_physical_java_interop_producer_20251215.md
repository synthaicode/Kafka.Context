# diff: Java (JVM) producer → C# DynamicTopicSet (physical)

## Summary
- Add a physical test that produces Avro+Schema Registry messages from the JVM side and consumes them in C# via `KafkaContext.Topic(...).ForEachAsync(...)`.

## Why
- Kafka/Schema Registry ecosystems are JVM-first (Java/Flink/ksqlDB). This test validates that C# consumption is compatible with JVM-produced Avro payloads and logical types.

## What the test does
- `tests/physical/Kafka.Context.PhysicalTests/JavaInteropProducerTests.cs`
  - Runs a small Java producer inside the existing `schema-registry` docker container (OpenJDK already available in Confluent images).
  - The Java producer uses Confluent `KafkaAvroSerializer` and registers the schema to Schema Registry.
  - C# consumes from the topic using `DynamicTopicSet` and asserts values/types for:
    - `boolean`, `int`, `long`, `string`
    - `timestamp-millis` → `DateTime`/`long` compatibility
    - `decimal` logical type
    - `map<string,string>`

## Notes
- This avoids installing a JDK on the host machine; the docker stack already includes Java runtime/tooling.

