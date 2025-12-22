# diff: Clarify Attach vs Provision for Streaming queries

## Decision
- Attach (connect to existing infra) uses only `EventSet<T>`; no `ToQuery` usage.
- Declaring `ToQuery` in `OnModelCreating` is a provisioning intent (queries to be registered by `ProvisionStreamingAsync`).

## Changes
- Remove `OutputTopic` from `QueryDefinition` in the spec; `ToQuery` registers only `(Type, Expression, QueryName)`.
- Clarify that source/topic name normalization and engine object wiring are dialect-provider concerns.

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
