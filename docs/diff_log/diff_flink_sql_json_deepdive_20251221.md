# diff: Deep dive Flink JSON array path behavior

## Summary
- Add focused probes to confirm JSON path extraction behavior, especially array element access on `$.a[i]`.
- Identify a common pitfall: over-escaping JSON string literals leads to unexpected NULL results.

## Findings (this Flink image)
- `JSON_VALUE('{"a":[10,20]}', '$.a[1]')` returns `20` (OK).
- Using escaped JSON like `'{\"a\":[10,20]}'` can produce NULL (avoid escaping double-quotes inside SQL string literals).
- `JSON_QUERY('{"a":[10,20]}', '$.a[1]')` returns NULL while `JSON_VALUE` works (treat `JSON_QUERY` support as limited/quirky).
- `JSON_TABLE` does not parse in this environment (syntax not accepted by the parser).

## Files
- Added: `features/flink-sql-research/sql/28_json_object_array_probe.sql`
- Added: `features/flink-sql-research/sql/29_json_paths_probe.sql`
- Added: `features/flink-sql-research/sql/30_json_table_probe.sql`
- Updated: `features/flink-sql-research/findings.md`
