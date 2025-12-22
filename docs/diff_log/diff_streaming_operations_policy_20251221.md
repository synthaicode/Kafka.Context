# diff: Define Streaming operational policy (names, idempotency, change handling)

## Summary
- Define a clear operational boundary: core `ProvisionAsync` handles Kafka/SR; `ProvisionStreamingAsync` only registers engine objects (no rollback, no drop).
- Split naming responsibilities between Kafka topics and engine object names (do not enforce `topic == stream/table name`).
- Define idempotency unit as `QueryName` and specify how to handle query definition changes.

## Motivation
- Make `CREATE IF NOT EXISTS` effective by stabilizing the engine object names.
- Prevent name collisions between Kafka topics and engine objects.
- Keep operations deterministic under “register-only / no rollback” provisioning.

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
