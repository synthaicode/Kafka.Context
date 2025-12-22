# streaming-flink

Flink dialect `ToQuery(...)` samples.

- Shows select / where / groupby / having / join separately (DDL is printed to stdout).

## Flink query support

- Window: TUMBLE / HOP / SESSION (`FlinkWindow` + `FlinkWindowExtensions`)
- Aggregates: `FlinkAgg.Count/Sum/Avg`
- Functions: `FlinkSql` string/JSON/datetime/array/map helpers (see `docs/wiki/streaming-api.md`)

## Run

```powershell
dotnet run --project examples/streaming-flink/FlinkStreamingExamples.csproj
```
