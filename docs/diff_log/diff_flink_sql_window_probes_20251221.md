# diff: flink sql window probes (2025-12-21)

## Summary

- Flink SQL の Window 系（TUMBLE/HOP/SESSION/OVER/PROCTIME）を調査し、batch/streaming の可否と制約を整理した。

## Added

- SQL probes:
  - `features/flink-sql-research/sql/60_window_hop.sql`
  - `features/flink-sql-research/sql/61_window_session.sql`
  - `features/flink-sql-research/sql/61b_window_session_streaming.sql`
  - `features/flink-sql-research/sql/62_window_over_rows.sql`
  - `features/flink-sql-research/sql/63_window_over_range.sql`
  - `features/flink-sql-research/sql/63b_window_over_range_numeric.sql`
  - `features/flink-sql-research/sql/64_window_proctime_tumble.sql`

## Findings

- TUMBLE/HOP:
  - batch runtime で Window TVF が動作する。
- SESSION:
  - batch runtime は NG（`Unaligned windows like session are not supported in batch mode yet.`）
  - streaming runtime は OK（changelog として `+I/-D` が出る）
- PROCTIME Window TVF:
  - NG（`Processing time Window TableFunction is not supported yet.`）
- OVER windows:
  - `ROWS` は実行できるが、この環境（datagen + sql-client）では結果が 1 行のみで要再検証。
  - `RANGE BETWEEN INTERVAL ...` は NG（`Unsupported temporal arithmetic`）
  - `ORDER BY BIGINT` の numeric range は実行できるが、同様に出力は 1 行のみで要再検証。

## Evidence (reports)

- `reports/flink_sql/20251221_171322_window_probes1/report.md`
- `reports/flink_sql/20251221_171508_window_probes2/report.md`

## Docs

- `features/flink-sql-research/findings.md` を更新。

