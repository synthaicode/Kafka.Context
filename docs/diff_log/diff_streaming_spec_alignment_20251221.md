# diff: Align streaming spec with master plan decisions

## Summary
- Treat Window as dialect-specific (not part of common Streaming API).
- Treat dialect selection API examples (e.g., `UseStreaming<TProvider>()`) as pseudo-code until the final integration point is fixed.
- Split `ToQuery` responsibilities: common layer records declarations; dialect layer overrides `ToQuery` to build logical plans and generate executable artifacts.
- Adopt `readOnly` / `writeOnly` entity modes (derived/query entities should be `readOnly: true`).

## Motivation
- Keep semantics-coupled features (time/window/emit/within) out of the common layer.
- Preserve a single modeling entry point (`OnModelCreating`) while delegating execution to the selected backend.
- Reduce accidental misuse by enforcing read/write intent at runtime (`AddAsync` vs `ForEachAsync`).

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
