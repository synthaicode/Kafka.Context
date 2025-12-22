# diff: Add Flink SQL research harness (docker + history)

## Summary
- Add a Flink SQL docker compose overlay and scripts to run multiple SQL files non-interactively.
- Persist every execution as a timestamped report under `reports/flink_sql/` (stdout/stderr + copied SQL).

## Motivation
- Flink dialect support requires validating which query shapes are accepted (JOIN / GROUP BY / HAVING / WINDOW, etc.).
- Keep experiments reproducible and reviewable by recording inputs (SQL) and outputs (logs) per run.

## Files
- Added: `features/flink-sql-research/`
- Added: `tools/flink/plugins/.gitignore`
