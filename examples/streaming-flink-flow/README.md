# streaming-flink-flow

`From(...)` で参照した入力 topic に `AddAsync` して、`ToQuery(...)` の出力 topic を `ForEachAsync` する流れの例。

## Run (dry-run)

DDL を標準出力に出して終了する（Flink 実行はしない）。

```powershell
dotnet run --project examples/streaming-flink-flow/FlinkStreamingFlowExample.csproj
```

## Run (with Kafka)

Kafka が起動している前提で、入力へ `AddAsync` し、出力を `ForEachAsync` で短時間だけ読む。

```powershell
dotnet run --project examples/streaming-flink-flow/FlinkStreamingFlowExample.csproj -- --run
```


## AI Assist
If you're unsure how to use this package, run `kafka-context ai guide --copy` and paste the output into your AI assistant.
