# Flink SQL run report

- Timestamp: 20251221_162939
- Name: type_probes6_fix
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 41_case_coalesce_nullif_flink_probe.sql | 0 | 41_case_coalesce_nullif_flink_probe.log | 41_case_coalesce_nullif_flink_probe.err.log |
| 46_decimal_overflow_probe.sql | 0 | 46_decimal_overflow_probe.log | 46_decimal_overflow_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

