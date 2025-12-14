# diff_dlq_no_raw_payload_20251214

## 背景
DLQ の標準形式（`DlqEnvelope`）を運用しやすくするため、DLQ に格納する情報量を確定する。

## 決定事項
- DLQ は raw payload を保持しない。
- 元トピックのメタ情報のみを保持する（topic/partition/offset・headers・エラー情報等）。

## 影響
- DLQ はサイズ/機密の観点で扱いやすくなる。
- payload 本体の保全が必要な場合は、別手段（再読込/外部保管）で対応する。

