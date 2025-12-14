# Contract: Naming (NuGet + root namespace)

## 達成できる結果
利用者が「この OSS は何を提供しているか」を NuGet と名前空間で即座に理解できる。

## 決定
- NuGet パッケージ名: `Kafka.Context`
- ルート名前空間: `Kafka.Context`

## 備考
- 設定セクション名（`KsqlDsl`）は運用資産継承のため維持する（`docs/contracts/appsettings.md`）。
- サポート対象フレームワークは .NET 8 / .NET 10 とする（`docs/diff_log/diff_target_frameworks_net8_net10_20251214.md`）。
