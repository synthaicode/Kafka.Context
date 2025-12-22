# Flink SQL run report

- Timestamp: 20251221_163241
- Name: func_probes7
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 47_array_map_probe.sql | 1 | 47_array_map_probe.log | 47_array_map_probe.err.log |
| 48_ifnull_nvl_probe.sql | 1 | 48_ifnull_nvl_probe.log | 48_ifnull_nvl_probe.err.log |
| 49_date_trunc_diff_probe.sql | 1 | 49_date_trunc_diff_probe.log | 49_date_trunc_diff_probe.err.log |
| 50_date_add_sub_probe.sql | 1 | 50_date_add_sub_probe.log | 50_date_add_sub_probe.err.log |
| 51_string_null_semantics_probe.sql | 0 | 51_string_null_semantics_probe.log | 51_string_null_semantics_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

