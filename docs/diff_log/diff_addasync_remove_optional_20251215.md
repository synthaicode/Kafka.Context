# diff: AddAsync removes optional CancellationToken

## Summary
- Replace `EventSet<T>.AddAsync(T entity, CancellationToken cancellationToken = default)` with explicit overloads:
  - `AddAsync(T entity)`
  - `AddAsync(T entity, CancellationToken cancellationToken)`

## Rationale
- Avoid optional-parameter backcompat pitfalls (default values are embedded at call sites).
- Align with PublicApiAnalyzers guidance and eliminate RS0027 warnings for this overload set.

## Impact
- Source-compatible for typical calls:
  - `AddAsync(entity)` continues to work.
  - `AddAsync(entity, token)` continues to work.
