# Flink SQL run report

- Timestamp: 20251221_162807
- Name: type_probes6
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 40_math_functions_flink_ok.sql | 0 | 40_math_functions_flink_ok.log | 40_math_functions_flink_ok.err.log |
| 41_case_coalesce_nullif_flink_probe.sql | 1 | 41_case_coalesce_nullif_flink_probe.log | 41_case_coalesce_nullif_flink_probe.err.log |
| 42_try_cast_probe.sql | 0 | 42_try_cast_probe.log | 42_try_cast_probe.err.log |
| 43_timestamp_parse_probe.sql | 0 | 43_timestamp_parse_probe.log | 43_timestamp_parse_probe.err.log |
| 44_from_unixtime_probe.sql | 0 | 44_from_unixtime_probe.log | 44_from_unixtime_probe.err.log |
| 45_substring_index_base_probe.sql | 0 | 45_substring_index_base_probe.log | 45_substring_index_base_probe.err.log |
| 46_decimal_overflow_probe.sql | 1 | 46_decimal_overflow_probe.log | 46_decimal_overflow_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

