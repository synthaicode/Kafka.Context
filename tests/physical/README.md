# tests/physical/

Docker を前提にした physical test を置く。

環境は `docs/environment/docker-compose.current.yml` を参照する。

## 実行手順
- Docker を起動する: `docker compose -f docs/environment/docker-compose.current.yml up -d`
- physical test を有効化する: `setx KAFKA_CONTEXT_PHYSICAL 1`（PowerShell の場合は新しいシェルを開く）
- 実行する: `dotnet test tests/physical/Kafka.Context.PhysicalTests/Kafka.Context.PhysicalTests.csproj -c Release`
