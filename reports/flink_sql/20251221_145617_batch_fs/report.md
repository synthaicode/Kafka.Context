# Flink SQL run report

- Timestamp: 20251221_145617
- Name: batch_fs
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: $composeKafka + $composeFlink"
# Flink SQL run report  - Timestamp: 20251221_145617 - Name: batch_fs - Docker: 28.4.0 - Flink image: apache/flink:1.19.1-scala_2.12-java17 += "
# Flink SQL run report  - Timestamp: 20251221_145617 - Name: batch_fs - Docker: 28.4.0 - Flink image: apache/flink:1.19.1-scala_2.12-java17 += 

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 01_basic_select.sql | 1 | 01_basic_select.log | 01_basic_select.err.log |
| 02_join.sql | 1 | 02_join.log | 02_join.err.log |
| 03_groupby.sql | 1 | 03_groupby.log | 03_groupby.err.log |
| 04_having.sql | 1 | 04_having.log | 04_having.err.log |
| 05_window_tumble.sql | 1 | 05_window_tumble.log | 05_window_tumble.err.log |

## Notes
- SQL files are copied into this directory for traceability.
