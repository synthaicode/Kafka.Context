# diff: Drop read/write entity modes + make StreamingQueryRegistry instance-scoped

## Summary
- Stop adopting entity access modes (`readOnly` / `writeOnly`) for Streaming; treat enforcement as out of scope.
- Revert core API to `IModelBuilder.Entity<T>()` (no flags) and remove runtime enforcement in `EventSet<T>`.
- Change Streaming query registry from a static global store to a per-`KafkaContext` instance store.
- Record the **core API** change as **Breaking Change** in `CHANGELOG.md`.

## Motivation
- Keep the core API small and avoid “role modeling” that can become a leaky abstraction for streaming queries.
- Avoid global mutable state (`static` registry), which breaks multi-context usage and parallel test execution.
- Make Streaming query declaration safe and deterministic by scoping query definitions to a context instance.

## Affected files
- Updated: `src/Kafka.Context.Abstractions/Model/IModelBuilder.cs`
- Updated: `src/Kafka.Context/EventSet.cs`
- Updated: `src/Kafka.Context/KafkaContext.cs`
- Updated: `docs/kafka-context-master-plan.md`
- Updated: `docs/kafka-context-streaming-spec.md`
- Updated: `CHANGELOG.md`
