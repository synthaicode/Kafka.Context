# diff_release_flow_gpr_to_nuget_20251214

## 目的
Kafka.Context のリリース手順を「GitHub Packages へ RC publish → DL/動作確認 → nuget.org へ stable publish」の流れとして固定する。

## 変更概要
- `docs/workflows/release_roles_and_steps.md` の release flow を RC download & verify 前提に更新。
- RC publish（GitHub Packages）と stable publish（nuget.org）の手順を具体的なコマンド例で明記。

