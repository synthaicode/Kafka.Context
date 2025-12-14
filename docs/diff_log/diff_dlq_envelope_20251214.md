# diff_dlq_envelope_20251214

## 背景
MVP の成功条件に「失敗時 DLQ」を含めるため、DLQ の標準メッセージ形式を確定する。

## 決定事項
- DLQ の value は `DlqEnvelope` を標準とする（詳細は `docs/contracts/dlq.md`）。
- key は `{Topic, Partition, Offset}` の複合キーとする。

## 目的
- 再処理・調査で「どの原典レコードか」「何が起きたか」を最小コストで辿れるようにする。

## 未解決の論点（次アクション）
- raw payload の保全方針（標準に含めるか、別途にするか）

## Update（後続diffで解決）
- raw payload は保持しない: `docs/diff_log/diff_dlq_no_raw_payload_20251214.md`
