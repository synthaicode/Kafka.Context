# diff: DynamicTopicSet supports ksqlDB windowed keys

## Summary
- Handle ksqlDB windowed keys where window bytes are appended after the Confluent Avro payload.
- Add `Kafka.Context.Messaging.WindowedKey` to expose:
  - decoded Avro key record (`GenericRecord`)
  - window boundary metadata extracted from the trailing bytes

## Rationale
- Tumbling/Hopping results often use windowed keys; the raw key payload is not always a pure Confluent Avro blob.
- Without stripping the trailing window bytes, Avro key deserialization can fail.

## Impact
- `DynamicTopicSet` now returns `WindowedKey` as `key` for windowed-key messages.

