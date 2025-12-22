# diff: Dialect-side ObjectName normalization + duplicates policy + loop guard

## Decisions
- ObjectName normalization is interpreted by dialect providers (identifier rules, quoting, reserved words, length limits).
- Allow duplicate `QueryDefinition` registrations for the same To-side (same output topic), because the dialect may emit INSERT-based registrations.
- If the dialect selects CTAS/creation, duplicate creation for the same ObjectName is Fail-Fast.
- Definition changes (expression updates) are handled operationally and are out of scope for this spec.
- Add a loop guard: if any input topic equals the output topic, provisioning must Fail-Fast.

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
- Updated: `docs/kafka-context-master-plan.md`
