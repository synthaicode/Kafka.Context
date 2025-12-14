# diff_mvp_scope_20251214

## 背景
Kafka + Schema Registry を「Context中心・契約駆動」で扱える軽量ランタイムとして実装/公開するため、進め方とスコープを確定する。

## 決定事項
- 本構想は `Ksql.Linq` 本体とは別リポジトリとして進める。
- `Ksql.Linq` 側で育てた資産（ドキュメント運用、ワークフロー、設計/命名の知見）は活用する。
- ランタイムの責務に Provisioning（Topic作成、Schema Registry 登録、互換性・命名規約の確定）を含める。
  - 失敗した場合は起動しない（実行前に破綻を防ぐ）。
- API は最小に畳む（I/O中心）。
  - 書き込み: `AddAsync(key, poco)`
  - 読み取り: `ForEachAsync(handler)`
  - 失敗時: Retry / DLQ / 再処理
  - DLQ の読み取りは「DLQ に対する `ForEachAsync`」で行う。

## 成功条件（MVP）
- Context作成 → SR登録 → `AddAsync`/`ForEachAsync` → 失敗時 DLQ

## Non-Goals（明示）
- ストリームクエリ（Window / JOIN / 集約）
- 実行フレームワーク（Bus / Worker / Throttle 等）
- KafkaFlow の代替（否定ではなく別の上位抽象）

## 未解決の論点（次アクション）
- 契約形式（Avro/Protobuf/JSON Schema）と Subject 命名規約（例: `{topic}-value`）の採用
- Retry のモデル（in-process / retry topic / backoff 方式）とデフォルト値
- DLQ への格納形式（元メッセージ + 失敗理由メタデータの標準化）

