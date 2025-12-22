# diff: Flink connector DDL is Avro-only

## Summary
- Set Flink Kafka connector DDL generation to Avro-only (`format = 'avro-confluent'`).
- Added fail-fast validation: any other `FlinkKafkaConnectorOptions.Format` is rejected.
- Updated docs to reflect the Avro-only policy.

## Motivation
- Keep the initial Flink implementation constrained to a single, well-defined serialization format aligned with Schema Registry usage.

## Files
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkKafkaConnectorOptions.cs`
- Updated: `src/Kafka.Context/Streaming/Flink/FlinkDialectProvider.cs`
- Updated: `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs`
- Updated: `docs/wiki/streaming-api.md`
- Updated: `docs/kafka-context-streaming-spec.md`
