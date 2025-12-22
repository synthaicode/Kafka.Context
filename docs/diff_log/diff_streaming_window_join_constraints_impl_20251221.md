# diff: streaming window join constraints impl (2025-12-21)

## Summary

- Window+JOIN の確定ルールを `KafkaContext.ProvisionStreamingAsync` の事前検証として実装し、Fail-Fast でガードするようにした。

## Implemented Constraints

- JOIN が存在する場合:
  - JOIN の `ON` はキー等値のみ（`==` のみ、または `&&` で結合した等値のみ）
  - 現クエリ自体が CTAS（= GroupBy/Aggregate/Having を含む）なら禁止（JOIN→集計/Window を禁止）
  - SourceTypes のうち **ちょうど1つ** が CTAS 由来の Derived であること（stream-stream / CTAS×CTAS を禁止）
  - CTAS 以外の Derived を join 側に混ぜない

## Notes

- このガードは Window 実装前の段階でも適用されるため、JOIN サンプルは「CTAS 片側 + JOIN」に合わせて更新した。

## Changes

- `src/Kafka.Context/KafkaContext.cs`
- `tests/unit/Kafka.Context.Tests/StreamingProvisioningTests.cs`
- `examples/streaming-flink/Program.cs`
- `examples/streaming-flink-flow/Program.cs`

