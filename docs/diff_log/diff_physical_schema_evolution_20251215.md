# diff: Schema evolution (Schema Registry compatibility) physical test

## Summary
- Add a physical test that validates Schema Registry compatibility + schema evolution and confirms that an older C# POCO can keep consuming data produced with the evolved schema.

## Compatibility rule (explicit)
- Subject naming strategy is **TopicNameStrategy** (`{topic}-value`).
- The test sets **per-subject** compatibility to `FULL` via Schema Registry REST API (`PUT /config/{subject}`).

## Evolution scenario
- V1 → V2: **add a new field with a default**, without changing `precision/scale` for decimal or the `timestamp-millis` logical type.
  - V2 adds `NewField` with `"default":"v2"`.
  - Registration is validated via SR (`POST /subjects/{subject}/versions`, `GET /subjects/{subject}/versions/latest`).

## Consumer CLR expectations (fixed by this test)
- Consumption is done via `EventSet<T>` (POCO mapping):
  - `timestamp-millis` → `DateTime` (`[KafkaTimestamp]`)
  - `decimal` logical type → `decimal` (`[KafkaDecimal]`)
  - `map<string,string>` → `Dictionary<string,string>`
- Extra fields in the writer schema (V2) are ignored by the V1 POCO mapping.

## Failure diagnostics
- Test prints SR `subject/version/id` and Kafka consume meta (`topic/partition/offset/timestamp`) to make cross-system failures (JVM/SR/C#) easier to isolate.

