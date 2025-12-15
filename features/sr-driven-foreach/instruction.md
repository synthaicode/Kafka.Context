# feature: SR-driven ForEachAsync (bridge mode)

## Goal
Consume external-system output (Flink/ksqlDB/etc.) where Schema Registry is the source of truth, and handle records in C# without requiring compile-time POCOs.

## Scope
- Add a new consume entry point by topic name: `KafkaContext.Topic(string topic)`.
- Provide handler signature: `ForEachAsync((key, value, headers, meta) => ...)`.
- Allow users to insert POCO mapping logic on top of the raw key/value objects.

## Contracts (current decision)
- Value:
  - Must be Confluent Avro payload; decode to `GenericRecord`.
- Key:
  - If Confluent Avro payload: decode to `GenericRecord` (including window boundaries for tumbling/hopping keys).
  - Otherwise: pass-through as `byte[]` (SR-unregistered primitive/unknown encoding).

## Notes
- Error handling uses the same Retry/DLQ policy surface as `EventSet<T>`.
- Manual commit uses `Commit(MessageMeta meta)` when `autoCommit=false`.

