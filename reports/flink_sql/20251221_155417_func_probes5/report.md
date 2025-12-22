# Flink SQL run report

- Timestamp: 20251221_155417
- Name: func_probes5
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 01_basic_select.sql | 0 | 01_basic_select.log | 01_basic_select.err.log |
| 02_join.sql | 0 | 02_join.log | 02_join.err.log |
| 03_groupby.sql | 0 | 03_groupby.log | 03_groupby.err.log |
| 04_having.sql | 0 | 04_having.log | 04_having.err.log |
| 05_window_tumble.sql | 0 | 05_window_tumble.log | 05_window_tumble.err.log |
| 10_string_flink_ok.sql | 0 | 10_string_flink_ok.log | 10_string_flink_ok.err.log |
| 11_datetime_extract_flink_ok.sql | 0 | 11_datetime_extract_flink_ok.log | 11_datetime_extract_flink_ok.err.log |
| 12_datetime_format_flink_ok.sql | 0 | 12_datetime_format_flink_ok.log | 12_datetime_format_flink_ok.err.log |
| 13_cast_decimal_flink_ok.sql | 0 | 13_cast_decimal_flink_ok.log | 13_cast_decimal_flink_ok.err.log |
| 14_json_flink_probe.sql | 0 | 14_json_flink_probe.log | 14_json_flink_probe.err.log |
| 15_split_lpad_rpad_probe.sql | 0 | 15_split_lpad_rpad_probe.log | 15_split_lpad_rpad_probe.err.log |
| 16_split_index_probe.sql | 0 | 16_split_index_probe.log | 16_split_index_probe.err.log |
| 17_date_parts_functions_probe.sql | 0 | 17_date_parts_functions_probe.log | 17_date_parts_functions_probe.err.log |
| 18_weekofyear_probe.sql | 0 | 18_weekofyear_probe.log | 18_weekofyear_probe.err.log |
| 19_nested_aggregate_wrappers.sql | 0 | 19_nested_aggregate_wrappers.log | 19_nested_aggregate_wrappers.err.log |
| 20_decimal_ops_agg_probe.sql | 0 | 20_decimal_ops_agg_probe.log | 20_decimal_ops_agg_probe.err.log |
| 21_split_fail.sql | 1 | 21_split_fail.log | 21_split_fail.err.log |
| 22_contains_startswith_endswith.sql | 0 | 22_contains_startswith_endswith.log | 22_contains_startswith_endswith.err.log |
| 23_regex_like_extract.sql | 1 | 23_regex_like_extract.log | 23_regex_like_extract.err.log |
| 24_uuid_guid_probe.sql | 0 | 24_uuid_guid_probe.log | 24_uuid_guid_probe.err.log |
| 25_timestamp_ltz_timezone_probe.sql | 0 | 25_timestamp_ltz_timezone_probe.log | 25_timestamp_ltz_timezone_probe.err.log |
| 26_json_array_probe.sql | 1 | 26_json_array_probe.log | 26_json_array_probe.err.log |
| 90_ksql_instr_fail.sql | 0 | 90_ksql_instr_fail.log | 90_ksql_instr_fail.err.log |
| 91_ksql_len_fail.sql | 1 | 91_ksql_len_fail.log | 91_ksql_len_fail.err.log |
| 92_ksql_timestampToString_fail.sql | 1 | 92_ksql_timestampToString_fail.log | 92_ksql_timestampToString_fail.err.log |
| 93_ksql_dateadd_fail.sql | 1 | 93_ksql_dateadd_fail.log | 93_ksql_dateadd_fail.err.log |
| 94_ksql_json_extract_string_fail.sql | 1 | 94_ksql_json_extract_string_fail.log | 94_ksql_json_extract_string_fail.err.log |
| 95_ksql_ucase_lcase_probe.sql | 1 | 95_ksql_ucase_lcase_probe.log | 95_ksql_ucase_lcase_probe.err.log |
| 96_ksql_format_timestamp_fail.sql | 1 | 96_ksql_format_timestamp_fail.log | 96_ksql_format_timestamp_fail.err.log |
| 97_ksql_json_array_length_fail.sql | 1 | 97_ksql_json_array_length_fail.log | 97_ksql_json_array_length_fail.err.log |
| 98_ksql_earliest_latest_fail.sql | 1 | 98_ksql_earliest_latest_fail.log | 98_ksql_earliest_latest_fail.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

