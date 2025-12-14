# diff_dlq_timestamp_empty_20251214

## 背景
DLQ で timestamp が取れないケース（mapping 失敗など）でも、固定した規約で扱えるようにする。

## 決定事項
- 元レコードの timestamp が取得できない場合、`DlqEnvelope.TimestampUtc` は空文字（`""`）とする。

