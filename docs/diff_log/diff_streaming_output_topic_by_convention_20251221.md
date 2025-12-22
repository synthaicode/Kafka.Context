# diff: Output topic is derived from To-class name by convention

## Decision
- The output endpoint topic is derived from the **To-side class name** by convention (kebab-case).
- If a different endpoint is required, override it with `KafkaTopicAttribute` on the To-side class.

## Rationale
- Endpoints must be stable, but forcing explicit attributes everywhere is noisy.
- A deterministic convention keeps provisioning idempotent and easy to manage.
- Overrides allow alignment with existing infrastructure naming.

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
- Updated: `docs/kafka-context-master-plan.md`
