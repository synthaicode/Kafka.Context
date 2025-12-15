# diff: Physical test for manual commit restart

## Summary
- Add a physical test that verifies manual-commit offset persistence across a “restart” (new context, same consumer group).
- Add a cancellable overload for `ForEachAsync(..., autoCommit:false, CancellationToken)` so physical tests can stop cleanly.

## Rationale
- Confirm that manual `Commit(meta)` advances the consumer group offset and that a restarted consumer resumes from the committed position.

## Impact
- Public API addition (new `ForEachAsync` overload with `CancellationToken`).
- Physical tests updated/extended; examples updated to show producing with headers.
