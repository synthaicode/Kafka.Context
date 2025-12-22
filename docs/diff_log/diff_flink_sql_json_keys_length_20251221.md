# diff: Probe Flink JSON keys/length helpers

## Summary
- Add probes for `JSON_LENGTH`, `JSON_ARRAY_LENGTH`, `JSON_KEYS`, `JSON_OBJECT_KEYS`, and `JSON_EXISTS`.

## Findings (this Flink image)
- `JSON_EXISTS(json, path)` is available and works for `$.a[i]`.
- `JSON_LENGTH`, `JSON_ARRAY_LENGTH`, `JSON_KEYS`, `JSON_OBJECT_KEYS` are not available (validator "No match found for function signature ...").

## Files
- Added: `features/flink-sql-research/sql/31_json_length_probe.sql`
- Added: `features/flink-sql-research/sql/32_json_array_length_probe.sql`
- Added: `features/flink-sql-research/sql/33_json_keys_probe.sql`
- Added: `features/flink-sql-research/sql/34_json_object_keys_probe.sql`
- Added: `features/flink-sql-research/sql/36_json_exists_probe.sql`
- Updated: `features/flink-sql-research/findings.md`
- Updated: `features/flink-sql-research/run.ps1`
