# Flink SQL run report

- Timestamp: 20251221_163956
- Name: tz_probe
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 52b_table_local_time_zone_probe.sql | 0 | 52b_table_local_time_zone_probe.log | 52b_table_local_time_zone_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

