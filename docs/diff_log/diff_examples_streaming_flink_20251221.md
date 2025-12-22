# diff: examples streaming flink (2025-12-21)

## Summary

- Flink 方言の `ToQuery(...)` サンプルを examples に追加した（select / where / groupby / having / join）。

## Added

- `examples/streaming-flink/README.md`
- `examples/streaming-flink/Program.cs`
- `examples/streaming-flink/FlinkStreamingExamples.csproj`

## Notes

- 実行はサンプル内の `FlinkDialectProvider` executor を `Console.WriteLine` にしているため、DDL が標準出力へ出る。

