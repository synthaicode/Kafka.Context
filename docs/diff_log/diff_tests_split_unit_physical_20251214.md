# diff_tests_split_unit_physical_20251214

## 背景
Kafka.Context は「純粋なロジック検証」と「Docker を伴う物理検証」を分離し、CI/ローカルの運用を簡単にする。

## 決定事項
- テストは `tests/unit` と `tests/physical` に分ける。
  - `tests/unit`: .NET の unit test（高速・外部依存なし）
  - `tests/physical`: Docker を前提とした physical test（Kafka/SR など）

