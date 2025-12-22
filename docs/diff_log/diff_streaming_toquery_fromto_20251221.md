# diff: Use `Entity<TDerived>().ToQuery(...From<T>()...)` to make From/To explicit

## Decision
- Use `Entity<TDerived>().ToQuery(q => q.From<TSource>()...)` as the canonical Streaming query declaration style.
- Fix the output endpoint via `KafkaTopicAttribute` on `TDerived`.

## Rationale
- `Entity<TDerived>()` expresses **To** (output) and `From<TSource>()` expresses **From** (inputs) directly in code.
- Output endpoints must be fixed for a system to be meaningful; `KafkaTopicAttribute` provides that stable contract.
- Engine object naming (`ObjectName`) can be derived from the output topic by a simple normalization rule, enabling idempotent `CREATE IF NOT EXISTS`.

## Consequences
- Drop the previous “Derived is not an entity” modeling direction; Derived is modeled as the To-side entity.
- Do not use a separate “StreamingObject” naming attribute; derive engine object names from output topics (dialect may apply quoting/normalization).

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
- Updated: `docs/kafka-context-master-plan.md`
