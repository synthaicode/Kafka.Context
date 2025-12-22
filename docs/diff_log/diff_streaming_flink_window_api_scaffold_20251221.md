# diff: streaming flink window api scaffold (2025-12-21)

## Summary

- Flink 方言で TUMBLE/HOP を先行対応するための Window API（plan metadata + 拡張メソッド）を追加した。

## Added

- `Kafka.Context.Abstractions`:
  - `StreamingWindowKind` / `StreamingWindowSpec`
  - `StreamingQueryPlan.Window`
  - `Kafka.Context.Streaming.Flink.FlinkWindow`（`window_start/window_end` の参照用）
  - `Kafka.Context.Streaming.Flink.FlinkWindowExtensions`（`TumbleWindow/HopWindow`）
- `Kafka.Context`:
  - `FlinkSqlRenderer` が `TABLE(TUMBLE(...))` / `TABLE(HOP(...))` を生成

## Notes

- Window TVF と JOIN の同一クエリ内併用は方言側で禁止（Fail-Fast）。
- interval は `TimeSpan` から `INTERVAL 'n' UNIT` へ単純変換（day/hour/minute/second/millisecond）。

## Tests

- `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs` に TUMBLE 出力の smoke test を追加。

