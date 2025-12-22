# Flink SQL run report

- Timestamp: 20251221_160816
- Name: json_deep_28
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 28_json_object_array_probe.sql | 0 | 28_json_object_array_probe.log | 28_json_object_array_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

