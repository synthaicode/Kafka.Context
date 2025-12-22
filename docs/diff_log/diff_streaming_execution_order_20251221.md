# diff: Define execution order for ProvisionStreamingAsync (duplicates policy)

## Summary
- INSERT（STREAM）は宣言順に実行する（重複は許容）。
- CTAS（TABLE）は、実行前の事前検証で同一 `ObjectName` の重複を検出したら Fail-Fast（1件も実行しない）。

## Motivation
- INSERT はファンイン（複数クエリ→同一出力）になり得るため、順序保証が必要。
- CTAS は同一 `ObjectName` の二重作成が不整合の原因になるため、開始前に検出して止める。

## Files
- Updated: `docs/kafka-context-streaming-spec.md`
