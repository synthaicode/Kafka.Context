# diff: Ksql.Linq basic queries + type coverage (physical)

## Summary
- Add additional ksqlDB physical coverage using `Ksql.Linq` and `KafkaContext.Topic(...).ForEachAsync(...)`.
- Validate the set of CLR types currently supported by this OSS for Avro + Schema Registry contracts.

## What was added
- `tests/physical/Kafka.Context.PhysicalTests/DynamicTopicSetKsqlLinqAllClrTypesTests.cs`
  - ksqlDB passthrough stream (`CREATE STREAM AS SELECT ... EMIT CHANGES`) backed by Avro + Schema Registry.
  - Produces an event containing:
    - `bool`, `int`, `long`, `string`, `DateTime` (`timestamp-millis`), `decimal` (`logicalType=decimal`), `Dictionary<string,string>` (`map`)
    - nullable variants for each of the above.
  - Consumes with `DynamicTopicSet` and asserts values/types.
- `tests/physical/Kafka.Context.PhysicalTests/AssemblyInfo.cs`
  - Disable xUnit parallelization **within the physical test project only** to avoid cross-test interference with shared external resources (ksqlDB/Kafka/SR).

## Bug fixes discovered by the test
- Fix timestamp serialization for Avro logical type `timestamp-millis`:
  - Producer now writes `DateTime` (UTC) as the logical value (instead of raw `long`), matching Apache Avro expectations.
  - Mapper accepts both `DateTime` and `long` for `DateTime` properties for compatibility.
- Extend map handling to accept `object[]` representations observed from ksqlDB Avro payloads.
