# diff: Manual commit uses MessageMeta

## Summary
- Replace `EventSet<T>.Commit(T entity)` with `EventSet<T>.Commit(MessageMeta meta)`.
- Internal commit tracking is now keyed by `MessageMeta` (topic/partition/offset), not by the mapped POCO instance.

## Rationale
- `Commit(entity)` is fragile if the handler transforms/copies the entity or if mapping changes the instance identity.
- `MessageMeta` is the stable identifier for what should be committed.

## Migration
- Before: `context.Orders.Commit(order);`
- After: `context.Orders.Commit(meta);` (use the `meta` parameter from `ForEachAsync((entity, headers, meta) => ...)`)

## Impact
- Breaking change for manual-commit consumers.
- No behavior changes for `autoCommit:true`.
