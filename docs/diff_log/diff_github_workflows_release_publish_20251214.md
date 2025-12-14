# diff_github_workflows_release_publish_20251214

## 目的
Kafka.Context のリリースフロー（GitHub Packages RC → DL/検証 → nuget.org stable）を CI で再現可能にする。

## 変更概要
- `.github/workflows/publish-preview.yml`
  - `release/**` push をトリガに、`<version>-rc<runNumber>` を pack して GitHub Packages に publish
  - Unit test を実行（physical は CI では実行しない）
- `.github/workflows/nuget-publish.yml`
  - `v*.*.*` tag push をトリガに stable を pack して nuget.org に publish（`secrets.NUGET_API_KEY` 必須）
- `docs/workflows/release_roles_and_steps.md` に workflow 名を追記

