# diff: examples streaming flink flow (2025-12-21)

## Summary

- `AddAsync`（入力 topic）→ `ForEachAsync`（`ToQuery` の出力 topic）の流れを示す Flink 向けサンプルを追加した。

## Added

- `examples/streaming-flink-flow/README.md`
- `examples/streaming-flink-flow/Program.cs`
- `examples/streaming-flink-flow/FlinkStreamingFlowExample.csproj`

## Notes

- デフォルトは dry-run（DDL を標準出力に出すだけ）。
- 実際に動かす場合は `--run` を付け、Flink DDL の実行部（TODO）を SQL Gateway/REST 等に差し替える。

