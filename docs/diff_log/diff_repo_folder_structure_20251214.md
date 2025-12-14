# diff_repo_folder_structure_20251214

## 背景
MVP 実装を開始するにあたり、成果物（パッケージ/サンプル/テスト/ドキュメント）の置き場を先に固定する。

## 決定事項
- リポジトリの主要フォルダ構成を以下で固定する。
  - `src/`: NuGet のソース（`Kafka.Context` をルート名前空間に寄せる）
  - `tests/`: ユニットテスト
  - `examples/`: 利用サンプル（QuickStart/ManualCommit/DLQ 等）
  - `docs/`: 契約・環境・運用（既存）
  - `features/`: 機能単位の作業（既存）

## 参照
- `docs/repository_structure.md`

