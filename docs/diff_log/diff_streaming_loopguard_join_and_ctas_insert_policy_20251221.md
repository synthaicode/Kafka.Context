# diff: Loop guard covers JOIN sources + CTAS/INSERT object type policy

## Decisions
- Loop guard checks all input sources involved in a query (From + Join). If any input topic equals the output topic, provisioning fails fast.
- Object type policy: CTAS produces TABLE, INSERT targets STREAM (dialect decides based on query shape/intent).

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
- Updated: `docs/kafka-context-master-plan.md`
