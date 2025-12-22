# Flink SQL run report

- Timestamp: 20251221_155918
- Name: regex_extract_only
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 23_regex_like_extract.sql | 0 | 23_regex_like_extract.log | 23_regex_like_extract.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

