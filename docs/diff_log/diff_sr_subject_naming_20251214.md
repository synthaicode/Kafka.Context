# diff_sr_subject_naming_20251214

## 背景
Kafka.Context の Provisioning（Schema Registry 登録/検証）を実装するため、Subject 命名規約を固定する。

## 決定事項
- Schema Registry の Subject 命名は topic ベースとする。
  - key: `{topic}-key`
  - value: `{topic}-value`

## 影響
- Provisioning では対象 topic 名から subject を決定し、存在/互換性を検証する。
- 既存運用で RecordNameStrategy 等を使っている環境では、移行時に subject のズレが起き得るため注意する。

