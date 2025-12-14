# diff_sr_compatibility_policy_20251214

## 背景
Kafka.Context の Provisioning（Schema Registry 登録/検証）において、互換性レベルの責務境界を確定する。

## 決定事項
- Kafka.Context は Schema Registry の互換性レベル（BACKWARD/FULL など）を「設定しない」。
- 互換性レベルは SR 側（クラスタ既定値 / subject 設定）に従う。
- Schema 登録時に非互換であれば SR が拒否し、Kafka.Context の起動（Provisioning）は失敗させる（fail fast）。

## 影響
- 互換性運用は SR の管理手順（環境/subject ごとの設定）に寄せる。
- Kafka.Context は「検証/登録の結果に従う」ため、互換性設定の変更は SR 側で行う。

