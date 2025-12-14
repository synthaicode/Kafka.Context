# diff_package_naming_20251214

## 背景
`Ksql.Linq` と別リポジトリとして公開するため、利用者が混同しない NuGet 名称とルート名前空間を確定する。

## 決定事項
- NuGet パッケージ名を `Kafka.Context` とする。
- ルート名前空間を `Kafka.Context` に寄せる。

## 影響
- ドキュメント/サンプルは `Kafka.Context.*` を前提に更新する。
- 設定セクション名は運用資産継承のため `KsqlDsl` を維持する（別途 diff で変更しない）。

