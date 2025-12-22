# Flink SQL run report

- Timestamp: 20251221_171322
- Name: window_probes1
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 60_window_hop.sql | 0 | 60_window_hop.log | 60_window_hop.err.log |
| 61_window_session.sql | 1 | 61_window_session.log | 61_window_session.err.log |
| 62_window_over_rows.sql | 0 | 62_window_over_rows.log | 62_window_over_rows.err.log |
| 63_window_over_range.sql | 1 | 63_window_over_range.log | 63_window_over_range.err.log |
| 64_window_proctime_tumble.sql | 1 | 64_window_proctime_tumble.log | 64_window_proctime_tumble.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

