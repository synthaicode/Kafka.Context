# diff_release_workflow_roles_steps_20251214

## 目的
Kafka.Context のリリース手順を「役割」「手順」「証跡」の観点で明文化し、属人化と手戻りを減らす。

## 変更概要
- `docs/workflows/release_roles_and_steps.md` を追加し、GO 判定・テスト・証跡・NuGet pack/publish の流れを定義。
- `docs/workflows/release.md` から上記ファイルへ誘導（詳細の正を一本化）。

