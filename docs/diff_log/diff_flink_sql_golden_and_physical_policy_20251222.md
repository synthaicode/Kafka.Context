# diff: Flink SQL verification policy

## Summary
- Require Golden (DDL generation UT) coverage for all queries listed in `reports/flink_sql`.
- Require full physical test coverage for the same set before release.

## Motivation
- Keep Flink dialect compatibility stable while controlling test cost.

## Files
- Updated: `docs/progress_management.md`
- Updated: `docs/workflows/release_roles_and_steps.md`
