# diff: streaming flink window constraints (2025-12-21)

## Summary

- Flink の Window（TUMBLE/HOP）先行対応に向けて、Window 利用時の制約（Fail-Fast）を実装した。

## Implemented

- `ProvisionStreamingAsync` 事前検証:
  - Window TVF は **単一入力のみ**（JOIN 不可）
  - Window TVF は **CTAS 必須**
  - `GroupBy` は **FlinkWindow.Start()/End() を両方含む** 必要がある

## Examples / Tests

- examples:
  - `examples/streaming-flink/Program.cs` に tumble/hop の宣言例を追加
- unit tests:
  - `tests/unit/Kafka.Context.Tests/StreamingProvisioningTests.cs` に window 制約のUTを追加

