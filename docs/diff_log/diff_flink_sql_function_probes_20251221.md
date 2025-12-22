# diff: Add Flink SQL probes for ksqlDB-derived functions

## Summary
- Add Flink SQL probe scripts that intentionally try ksqlDB function names/shapes and record success/failure via the existing docker harness.
- Focus areas: string functions, date/time extraction & formatting, casts/decimal, JSON extraction.

## Motivation
- Many LINQ expressions were historically translated into ksqlDB function names (see `C:\dev\Ksql.Linq.wiki/Function-Support.md`).
- Flink SQL is not ksqlDB; verifying which function names/shapes fail is required before implementing a Flink dialect provider.

## Files
- Added: `features/flink-sql-research/sql/10_string_flink_ok.sql`
- Added: `features/flink-sql-research/sql/11_datetime_extract_flink_ok.sql`
- Added: `features/flink-sql-research/sql/12_datetime_format_flink_ok.sql`
- Added: `features/flink-sql-research/sql/13_cast_decimal_flink_ok.sql`
- Added: `features/flink-sql-research/sql/14_json_flink_probe.sql`
- Added: `features/flink-sql-research/sql/90_ksql_instr_fail.sql`
- Added: `features/flink-sql-research/sql/91_ksql_len_fail.sql`
- Added: `features/flink-sql-research/sql/92_ksql_timestampToString_fail.sql`
- Added: `features/flink-sql-research/sql/93_ksql_dateadd_fail.sql`
- Added: `features/flink-sql-research/sql/94_ksql_json_extract_string_fail.sql`
- Added: `features/flink-sql-research/sql/95_ksql_ucase_lcase_probe.sql`
- Added: `features/flink-sql-research/findings.md`
