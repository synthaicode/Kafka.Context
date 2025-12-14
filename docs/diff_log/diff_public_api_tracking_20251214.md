# diff_public_api_tracking_20251214

## 目的
公開 API の変更をレビュー可能にし、破壊的変更の混入を防ぐために Public API ファイルを導入する。

## 変更概要
- 各パッケージ（`Kafka.Context*`）に `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` を追加。
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` を追加し、`StrictPublicApi=true` のときに RS0016/RS0017 を強制できるようにした。
- GitHub Actions（RC/stable publish）に Public API strict build を組み込んだ。

