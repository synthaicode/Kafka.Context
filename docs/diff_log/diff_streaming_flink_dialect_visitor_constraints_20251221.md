# diff: streaming flink dialect visitor constraints (2025-12-21)

## Summary

- Flink 方言の Visitor/Constraints（最小）を実装し、`ToQuery(...)` で受け取る式を SQL にレンダリングできる状態にした。

## Motivation

- 方言調査の結果（`features/flink-sql-research/findings.md`）を前提に、LINQ 式 → Flink SQL で成立する形へ変換する「入口」を用意する。
- 変換できないもの（例: `string.Split`）は fail-fast で拒否し、運用上の曖昧さを残さない。

## Changes

- Capture query expressions into plan:
  - `src/Kafka.Context.Abstractions/Streaming/StreamingQueryBuilder.cs`
  - `src/Kafka.Context.Abstractions/Streaming/StreamingQueryableExtensions.cs`
  - `src/Kafka.Context.Abstractions/Streaming/StreamingQueryPlan.cs`（追加プロパティ: predicate/selector/topics）
- Enrich plan with source topic names:
  - `src/Kafka.Context/KafkaContext.cs`（`SourceTopics` を `KafkaContext` 側で充填）
- Flink dialect provider + renderer:
  - `src/Kafka.Context/Streaming/Flink/FlinkDialectProvider.cs`
  - `src/Kafka.Context/Streaming/Flink/FlinkSqlRenderer.cs`

## Supported (initial)

- `WHERE` / `JOIN ON` の式:
  - 比較/論理演算、`COALESCE`
  - 文字列: `Length`→`CHAR_LENGTH`, `Substring`（0-based→1-based補正）, `Replace/Trim/Upper/Lower`, `IndexOf`→`POSITION-1`
  - `Contains/StartsWith/EndsWith`→`LIKE`（定数パターンは `ESCAPE '^'` で `%/_/^` をエスケープ）
- `SELECT new { ... }` / `new T { Prop = ... }` の射影（MemberInit/New の範囲）

## Not Supported (fail-fast)

- `string.Split`（Flink 側で `SPLIT` が使えない/互換性が低い前提。必要なら `SPLIT_INDEX` 相当の設計で別途対応）

## Evidence

- Unit tests:
  - `tests/unit/Kafka.Context.Tests/FlinkDialectProviderRenderingTests.cs`

