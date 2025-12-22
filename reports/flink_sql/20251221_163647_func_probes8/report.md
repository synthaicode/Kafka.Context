# Flink SQL run report

- Timestamp: 20251221_163647
- Name: func_probes8
- Docker: 28.4.0
- Flink image: apache/flink:1.19.1-scala_2.12-java17
- Compose: docs/environment/docker-compose.current.yml + features/flink-sql-research/docker-compose.flink.yml

## Results

| SQL | ExitCode | stdout | stderr |
|-----|----------|--------|--------|
| 52_timezone_at_time_zone_probe.sql | 1 | 52_timezone_at_time_zone_probe.log | 52_timezone_at_time_zone_probe.err.log |
| 53_epoch_millis_to_timestamp_probe.sql | 0 | 53_epoch_millis_to_timestamp_probe.log | 53_epoch_millis_to_timestamp_probe.err.log |
| 54_json_value_typed_probe.sql | 0 | 54_json_value_typed_probe.log | 54_json_value_typed_probe.err.log |
| 55_map_keys_values_probe.sql | 1 | 55_map_keys_values_probe.log | 55_map_keys_values_probe.err.log |
| 56_array_helpers_probe.sql | 0 | 56_array_helpers_probe.log | 56_array_helpers_probe.err.log |
| 57_like_escape_probe.sql | 1 | 57_like_escape_probe.log | 57_like_escape_probe.err.log |

## Notes
- SQL files are copied into this directory for traceability.
- Job IDs / states are recorded per SQL file (see below).

## Jobs

