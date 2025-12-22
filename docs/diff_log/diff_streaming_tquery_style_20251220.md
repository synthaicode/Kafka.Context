# diff: Adopt `Entity<T>().ToQuery(...)` as the Streaming modeling style

## Summary
- Align Streaming query declaration with the existing `OnModelCreating` model-building workflow.
- Use `modelBuilder.Entity<TDerived>().ToQuery(q => q.From<TSource>()...)` as the primary declaration style (similar to Kafka.Ksql.Linq examples).

## Motivation
- Keep a single, familiar modeling entry point (`KafkaContext.OnModelCreating(...)`).
- Lower learning cost by reusing the established `ToQuery` mental model.
- Make query discovery straightforward for provisioning and SQL preview generation.

## Implications
- `Kafka.Context.Abstractions.IModelBuilder` may need to evolve from `void Entity<T>()` to returning a builder type (e.g., `EntityModelBuilder<T>`) that can attach `ToQuery(...)`.
- Streaming query types become first-class “derived entities” in the model (registered the same way as input topics).

## Out of scope
- Final naming/shape of the window DSL types (`Windows`, `Tumbling(...)` etc.) is a placeholder here; it will follow the backend dialect package (`Kafka.Context.Streaming.Flink`, etc.).
