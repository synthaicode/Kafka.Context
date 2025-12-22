# Flink SQL 調査（docker 実行＋履歴化）

目的: Flink で有効な SQL（KSQL 相当の形を含む）を docker 上で実行し、成功/失敗を **reports に履歴化**する。

## 実行（最短）

1) Flink/Kafka/ksqlDB スタックを起動する

`pwsh -NoLogo -File features/flink-sql-research/run.ps1`

2) 出力ログを確認する

- 実行履歴: `reports/flink_sql/{timestamp}/report.md`
- 生ログ: `reports/flink_sql/{timestamp}/*.log`

## 使い方

### 既定（サンプル SQL 一括実行）

`pwsh -NoLogo -File features/flink-sql-research/run.ps1`

### 任意の SQL だけ実行

`pwsh -NoLogo -File features/flink-sql-research/run.ps1 -Sql features/flink-sql-research/sql/03_groupby.sql`

### Flink を停止

`pwsh -NoLogo -File features/flink-sql-research/run.ps1 -Down`

## 前提

- Docker Desktop（`docker compose` が動くこと）
- 既存の Kafka/SR/ksqlDB compose: `docs/environment/docker-compose.current.yml`

## 補足（非対話実行の制約）

- `sql-client.sh -f`（非対話）で `SELECT` の結果を表示するには、SQL ファイル内で `SET 'sql-client.execution.result-mode' = 'tableau';` を指定する
