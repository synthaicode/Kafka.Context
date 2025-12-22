# diff: flink sql array/date/null probes (2025-12-21)

## Summary

- Flink SQL の ARRAY/MAP、日付切り捨て、時刻差分、NULL 関連の probe を追加し、使用可否をレポート化した。

## Added

- SQL probes:
  - `features/flink-sql-research/sql/47_array_map_probe.sql`
  - `features/flink-sql-research/sql/47b_array_index_0_fail.sql`
  - `features/flink-sql-research/sql/48_ifnull_probe.sql`
  - `features/flink-sql-research/sql/48b_nvl_fail.sql`
  - `features/flink-sql-research/sql/49a_timestampdiff_probe.sql`
  - `features/flink-sql-research/sql/49b_trunc_floor_probe.sql`
  - `features/flink-sql-research/sql/50_date_add_sub_interval_probe.sql`
  - `features/flink-sql-research/sql/51_string_null_semantics_probe.sql`

## Findings

- ARRAY/MAP リテラルと `CARDINALITY` は使用できる。配列アクセスは **1-based**（`arr[0]` はエラー）。
- `IFNULL` は使用できるが `NVL` は使用できない。
- 日付の切り捨ては `FLOOR(ts TO DAY/HOUR)` が使用できる（`DATE_TRUNC('DAY', ts)` 形はシグネチャ不一致）。
- 差分は `TIMESTAMPDIFF(unit, t1, t2)` が使用できる。
- DATE/TIMESTAMP の加減算は `+/- INTERVAL` で表現できる（`DATE_ADD(date, 1)` 形はシグネチャ不一致）。
- 文字列関数は NULL を NULL として伝播する（`CONCAT/REPLACE/CHAR_LENGTH/TRIM`）。

## Evidence (reports)

- `reports/flink_sql/20251221_163241_func_probes7/report.md`（初回：複数を同一SQLに混在させたため失敗）
- `reports/flink_sql/20251221_163403_func_probes7_fix/report.md`（最終：OK/NG を分離して確定）

## Docs

- `features/flink-sql-research/findings.md` を更新。

