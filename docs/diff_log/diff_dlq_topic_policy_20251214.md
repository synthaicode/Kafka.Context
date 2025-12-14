# diff_dlq_topic_policy_20251214

## 背景
MVP の「失敗時 DLQ」を運用可能にするため、DLQ topic の持ち方と既定値を確定する。

## 決定事項
- 1 Context に 1 つの共通 DLQ topic を持つ（元 topic は `DlqEnvelope.Topic` に保持）。
- DLQ topic 名は強制せず、未設定時の既定を `dead_letter_queue` とする。
- DLQ topic は provisioning で作成/検証し、`retention.ms` と partition をオプションで指定できる。
- ランタイム初期化では DLQ を SR 登録対象に含めるが、デザイン時の出力では除外する。

