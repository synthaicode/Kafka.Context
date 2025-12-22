# diff: flink sql type probes (2025-12-21)

## Summary

- Flink SQL の「型/NULL/パース/安全キャスト」まわりの probe を追加し、使用可否をレポート化した。

## Added

- SQL probes:
  - `features/flink-sql-research/sql/40_math_functions_flink_ok.sql`
  - `features/flink-sql-research/sql/41_case_coalesce_nullif_flink_probe.sql`
  - `features/flink-sql-research/sql/42_try_cast_probe.sql`
  - `features/flink-sql-research/sql/43_timestamp_parse_probe.sql`
  - `features/flink-sql-research/sql/44_from_unixtime_probe.sql`
  - `features/flink-sql-research/sql/45_substring_index_base_probe.sql`
  - `features/flink-sql-research/sql/46_decimal_overflow_probe.sql`

## Findings

- `CASE` / `COALESCE` / `NULLIF` は使用できる。
- `TRY_CAST` は使用でき、変換に失敗した場合は `NULL` になる（数値の巨大リテラルはパース段階で失敗するため、必要なら文字列として与える）。
- `CAST('..' AS TIMESTAMP)` と `TO_TIMESTAMP('..')` が使用できる。
- `FROM_UNIXTIME(epochSeconds)` が使用できる。
- `SUBSTRING(s, 0, n)` は `SUBSTRING(s, 1, n)` 相当として動作する（ただし変換側で 0→1 に補正するのが安全）。

## Evidence (reports)

- `reports/flink_sql/20251221_162807_type_probes6/report.md`
- `reports/flink_sql/20251221_162939_type_probes6_fix/report.md`

## Docs

- `features/flink-sql-research/findings.md` に観測結果を追記。

