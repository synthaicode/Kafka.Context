# diff: Derived registration uses Streaming-only declarations (Option A)

## Decision
- Choose Option A: Derived (ToQuery output types) are **not** registered as Topic entities.
- Derived declarations only register query definitions into a per-context query registry.

## Rationale
- Avoid coupling derived query outputs to `KafkaTopicAttribute` and the core Topic/SR provisioning workflow.
- Keep Streaming provisioning as “register-only” and let the engine manage runtime artifacts (tables/views/sinks).
- Prevent accidental mixing of I/O (EventSet) concerns with derived query modeling.

## Consequences
- Use `IModelBuilder.Entity<T>()` only for input topics.
- Use `modelBuilder.ToQuery<TDerived>(registry, ...)` (Streaming extension) for derived outputs.
