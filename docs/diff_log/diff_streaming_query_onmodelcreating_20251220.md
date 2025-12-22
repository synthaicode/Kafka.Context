# diff: Declare Streaming queries in KafkaContext.OnModelCreating

## Summary
- Define Streaming queries as part of the model, declared inside `KafkaContext.OnModelCreating(...)`.
- Keep the primary entry API as `KafkaContext` / `EventSet<T>` to minimize learning cost; Streaming adds query declaration + provisioning/execution on top.

## Motivation
- Reduce API surface: avoid introducing a second “root context” (`StreamingContext`) just for queries.
- Align with EF Core mental model: “model configuration lives in `OnModelCreating`”.
- Enable fail-fast provisioning by collecting all declared queries in one place.

## Requirement (behavioral)
- Queries are declared at startup time (model building time), not dynamically at runtime.
- Query declarations must be discoverable for provisioning (DDL) and for previewing generated SQL.
- If a query cannot be translated to the selected backend dialect, it fails fast with a diagnostic error (no client-side fallback).

## Notes
- The master plan uses pseudo-code for `modelBuilder.Streaming(...)` / `streaming.Query(...)` as an API shape placeholder.
