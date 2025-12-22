# Flink SQL run report

- Timestamp: 20251221_171508
- Name: window_probes2
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 61b_window_session_streaming.sql | 0 | 61b_window_session_streaming.log | 61b_window_session_streaming.err.log |
| 63b_window_over_range_numeric.sql | 0 | 63b_window_over_range_numeric.log | 63b_window_over_range_numeric.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

