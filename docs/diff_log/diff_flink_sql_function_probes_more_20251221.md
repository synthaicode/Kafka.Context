# diff: Extend Flink SQL probes (string predicates, regex, JSON arrays, UUID, ksqlDB-only aggregates)

## Summary
- Add probes for `Contains/StartsWith/EndsWith`, regex extraction/predicate, JSON arrays, UUID/GUID, and ksqlDB-only aggregates.
- Capture results under `reports/flink_sql/*` with pass/fail for each SQL file.

## Notable findings (this Flink image)
- `LIKE` works for `Contains/StartsWith/EndsWith`.
- Regex: `REGEXP_EXTRACT` / `REGEXP_REPLACE` work; `SIMILAR TO` works; `REGEXP_LIKE` / `RLIKE` not available.
- JSON: `JSON_VALUE('[10,20,30]', '$[0]')` works; `JSON_VALUE('{"a":[...]}', '$.a[1]')` returns NULL (needs deeper investigation).
- `UUID()` exists; treat Guid as STRING unless Flink UUID type is confirmed.
- ksqlDB aggregates `EARLIEST_BY_OFFSET` / `LATEST_BY_OFFSET` are not available.

## Files
- Added: `features/flink-sql-research/sql/22_contains_startswith_endswith.sql`
- Added: `features/flink-sql-research/sql/23b_regex_predicate_similar_to_probe.sql`
- Added: `features/flink-sql-research/sql/24_uuid_guid_probe.sql`
- Added: `features/flink-sql-research/sql/25_timestamp_ltz_timezone_probe.sql`
- Added: `features/flink-sql-research/sql/26_json_array_probe.sql`
- Added: `features/flink-sql-research/sql/27_json_lax_strict_probe.sql`
- Added: `features/flink-sql-research/sql/97_ksql_json_array_length_fail.sql`
- Added: `features/flink-sql-research/sql/98_ksql_earliest_latest_fail.sql`
- Updated: `features/flink-sql-research/findings.md`
