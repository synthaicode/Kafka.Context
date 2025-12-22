# Flink SQL run report

- Timestamp: 20251221_153833
- Name: func_probes
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
| 05_window_tumble.sql | 1 | 05_window_tumble.log | 05_window_tumble.err.log |
| 10_string_flink_ok.sql | 1 | 10_string_flink_ok.log | 10_string_flink_ok.err.log |
| 11_datetime_extract_flink_ok.sql | 1 | 11_datetime_extract_flink_ok.log | 11_datetime_extract_flink_ok.err.log |
| 12_datetime_format_flink_ok.sql | 0 | 12_datetime_format_flink_ok.log | 12_datetime_format_flink_ok.err.log |
| 13_cast_decimal_flink_ok.sql | 0 | 13_cast_decimal_flink_ok.log | 13_cast_decimal_flink_ok.err.log |
| 14_json_flink_probe.sql | 0 | 14_json_flink_probe.log | 14_json_flink_probe.err.log |
| 90_ksql_instr_fail.sql | 0 | 90_ksql_instr_fail.log | 90_ksql_instr_fail.err.log |
| 91_ksql_len_fail.sql | 1 | 91_ksql_len_fail.log | 91_ksql_len_fail.err.log |
| 92_ksql_timestampToString_fail.sql | 1 | 92_ksql_timestampToString_fail.log | 92_ksql_timestampToString_fail.err.log |
| 93_ksql_dateadd_fail.sql | 1 | 93_ksql_dateadd_fail.log | 93_ksql_dateadd_fail.err.log |
| 94_ksql_json_extract_string_fail.sql | 1 | 94_ksql_json_extract_string_fail.log | 94_ksql_json_extract_string_fail.err.log |
| 95_ksql_ucase_lcase_probe.sql | 1 | 95_ksql_ucase_lcase_probe.log | 95_ksql_ucase_lcase_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

