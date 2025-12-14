# diff_nuget_readme_20251214

## 目的
NuGet ギャラリー上でパッケージ説明が表示されるように、`Kafka.Context` パッケージの README を同梱する。

## 変更概要
- `src/Kafka.Context/README.md` を追加（NuGet 表示用）。
- `src/Kafka.Context/Kafka.Context.csproj` に `PackageReadmeFile` と pack 対象ファイル設定を追加。

