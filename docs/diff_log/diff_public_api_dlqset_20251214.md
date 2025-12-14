# diff: public_api / DlqSet (2025-12-14)

## 変更理由
MVP の受入条件「DLQ を `Dlq.ForEachAsync` で読める」を満たすため、Context から DLQ topic を直接読むための入口を追加する。

## 変更内容
- `KafkaContext.Dlq`（`DlqSet`）を追加。
- `DlqSet.ForEachAsync(...)` を追加（`DlqEnvelope` を読む）。

## 補足
- DLQ topic は 1 Context = 1 topic（既定 `dead_letter_queue`、appsettings で変更可）。
- manual commit は MVP では record 単位のみ（`Commit(entity)` はハンドラ内で呼ぶ）。

