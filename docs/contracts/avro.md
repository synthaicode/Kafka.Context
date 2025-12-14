# Contract: Avro (Kafka.Context)

## 達成できる結果
Kafka + Schema Registry の「契約（schema）」を Avro で統一し、起動時に検証できる。

## 方針（移植元: Ksql.Linq.wiki）
- SerDes は Confluent 公式（`Confluent.SchemaRegistry.Serdes`）を使う。
- 安定スキーマ（コード生成型）は `ISpecificRecord`（SpecificRecord）を基本とする。
- 動的/一時的な検証用途は `GenericRecord` を許可する。
- Schema ID をハードコードしない（名前・namespace による解決を前提にする）。
- Avro decimal は `logicalType: "decimal"` を使い、precision/scale を厳密に合わせる。
- nullable（union）フィールドは未設定時に null を明示し、解決エラーを避ける。

## Subject 命名（決定）
- topic ベース
  - key: `{topic}-key`
  - value: `{topic}-value`

## key schema の登録方針（決定）
- key が **単一列** の場合: Kafka の key serializer（primitive）を前提とし、**Schema Registry に key subject を登録しない**。
- key が **複数列（複合キー）** の場合: Avro record として `{topic}-key` を登録する。

## 互換性（決定）
- Kafka.Context は互換性レベル（BACKWARD/FULL など）を設定しない。
- 互換性レベルは SR 側（クラスタ既定値 / subject 設定）に従う。
- 非互換の場合は登録時に SR が拒否し、Kafka.Context は Provisioning 失敗として起動しない。
