# Flink SQL run report

- Timestamp: 20251221_150850
- Name: basic_select_wait
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: $composeKafka + $composeFlink"
# Flink SQL run report  - Timestamp: 20251221_150850 - Name: basic_select_wait - Docker: 28.4.0 - Flink image: apache/flink:1.19.1-scala_2.12-java17 += "
# Flink SQL run report  - Timestamp: 20251221_150850 - Name: basic_select_wait - Docker: 28.4.0 - Flink image: apache/flink:1.19.1-scala_2.12-java17 += 

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 01_basic_select.sql | 0 | 01_basic_select.log | 01_basic_select.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

