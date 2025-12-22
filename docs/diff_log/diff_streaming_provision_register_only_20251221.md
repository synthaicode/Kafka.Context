# diff: ProvisionStreamingAsync is register-only (no rollback)

## Summary
- Define `ProvisionStreamingAsync` as a registration step only.
- Explicitly state that rollback is not performed when partial registration happens.

## Motivation
- Keep streaming provisioning simple and deterministic.
- Avoid complex/fragile rollback logic across engines.
- Rely on idempotent DDL patterns where possible.

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
