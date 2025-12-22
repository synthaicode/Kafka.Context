# diff: streaming window join constraints (2025-12-21)

## Summary

- Flink の Window（TUMBLE/HOP）を先行実装する前提で、Window と JOIN の併用ルールを確定した。

## Decision

- 許可（最小）:
  - 片側が Window 集計結果（CTAS）の場合のみ JOIN を許可
  - JOIN は INNER のみ
  - JOIN の `ON` はキー等値のみ
- 禁止（当面）:
  - stream-stream JOIN（両側が生入力/Window入力の JOIN）
  - JOIN → Window（JOIN 結果に Window 集計を適用）
  - Window 同士の JOIN（CTAS × CTAS）

## Spec Updates

- `docs/kafka-context-streaming-spec.md` に「Window + JOIN 制約（確定）」を追記。

