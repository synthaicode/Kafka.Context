# diff_dlq_fingerprint_20251214

## 背景
DLQ の重複検知・集計・診断で使える識別子を提供するため、fingerprint の算出規約を確定する。

## 決定事項
- `ErrorFingerprint` は `ErrorMessageShort + "\n" + StackTraceShort` の SHA-256 を大文字 HEX（`Convert.ToHexString`）で保持する。

## 未解決の論点（次アクション）
- `NormalizeStackTraceWhitespace` を fingerprint に反映するか（環境差/改行差への耐性 vs 一意性）。

## Update（後続diffで解決）
- 正規化を fingerprint に反映: `docs/diff_log/diff_dlq_fingerprint_normalize_stacktrace_20251214.md`
