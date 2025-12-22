# diff: flink sql timezone/map/like/json typed probes (2025-12-21)

## Summary

- Flink SQL の timezone/LIKE escape/MAP keys-values/JSON_VALUE typed について probe を追加し、方言変換の根拠を増やした。

## Added / Updated

- SQL probes:
  - `features/flink-sql-research/sql/52_timezone_at_time_zone_probe.sql`（NG: `AT TIME ZONE` / `TO_UTC_TIMESTAMP`）
  - `features/flink-sql-research/sql/52b_table_local_time_zone_probe.sql`（代替: `table.local-time-zone` + `TO_TIMESTAMP_LTZ`）
  - `features/flink-sql-research/sql/53_epoch_millis_to_timestamp_probe.sql`
  - `features/flink-sql-research/sql/54_json_value_typed_probe.sql`
  - `features/flink-sql-research/sql/55_map_keys_values_probe.sql`
  - `features/flink-sql-research/sql/56_array_helpers_probe.sql`
  - `features/flink-sql-research/sql/57_like_escape_probe.sql`

## Findings

- TimeZone 変換:
  - `AT TIME ZONE` は構文エラー、`TO_UTC_TIMESTAMP` はシグネチャなし。
  - 代替として `table.local-time-zone` を設定し、epoch を `TO_TIMESTAMP_LTZ` へ変換すればタイムゾーン反映が可能。
- JSON:
  - `JSON_VALUE` は **生の JSON 文字列**（`'{"a":10}'` 形）なら値取得できる。`{\"a\":10}` のようなエスケープを入れると NULL になり得る。
- MAP:
  - `MAP_KEYS` / `MAP_VALUES` は利用できる（ただしエイリアスに `Values` を使うとパースエラーになり得る）。
- LIKE:
  - `LIKE ... ESCAPE '^'` は利用できる（`ESCAPE '\\\\'` は runtime error になった）。

## Evidence (reports)

- `reports/flink_sql/20251221_163647_func_probes8/report.md`（初回）
- `reports/flink_sql/20251221_163849_func_probes8_fix/report.md`（fix）
- `reports/flink_sql/20251221_163956_tz_probe/report.md`（timezone代替確定）

## Docs

- `features/flink-sql-research/findings.md` を更新。

