# diff: Consume contract (no external schema-to-POCO mapping)

## Summary
- Document that `ForEachAsync` assumes the Avro contract matches the POCO (field name/type).
- Explicitly declare “external SR schema → POCO mapping layer” as a Non-Goal.

## Rationale
- This OSS uses Schema Registry to share contracts with downstream systems (e.g., Flink), but consume-side mapping should stay explicit and predictable.
- Fix expectations early to avoid accidental “it should auto-map external schemas” assumptions.

## Impact
- Documentation only (no runtime change).

