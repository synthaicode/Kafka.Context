# Flink SQL run report

- Timestamp: 20251221_151845
- Name: suite_select
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

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs


## Outputs

- Output base (in container): /out/20251221_151845_suite_select
- Copied outputs (host): out/
