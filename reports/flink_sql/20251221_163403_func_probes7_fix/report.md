# Flink SQL run report

- Timestamp: 20251221_163403
- Name: func_probes7_fix
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 47_array_map_probe.sql | 0 | 47_array_map_probe.log | 47_array_map_probe.err.log |
| 47b_array_index_0_fail.sql | 1 | 47b_array_index_0_fail.log | 47b_array_index_0_fail.err.log |
| 48_ifnull_probe.sql | 0 | 48_ifnull_probe.log | 48_ifnull_probe.err.log |
| 48b_nvl_fail.sql | 1 | 48b_nvl_fail.log | 48b_nvl_fail.err.log |
| 49a_timestampdiff_probe.sql | 0 | 49a_timestampdiff_probe.log | 49a_timestampdiff_probe.err.log |
| 49b_trunc_floor_probe.sql | 0 | 49b_trunc_floor_probe.log | 49b_trunc_floor_probe.err.log |
| 50_date_add_sub_interval_probe.sql | 0 | 50_date_add_sub_interval_probe.log | 50_date_add_sub_interval_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

