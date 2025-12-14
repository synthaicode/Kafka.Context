# diff_dlq_fingerprint_normalize_stacktrace_20251214

## 背景
DLQ fingerprint を「診断 ID」として環境差（改行/空白差）に強くし、同一障害の集約をしやすくする。

## 決定事項
- `NormalizeStackTraceWhitespace=true` の場合、`ErrorFingerprint` 計算前に `StackTraceShort` の空白（改行・連続空白等）を正規化する。

