# diff_release_workflow_local_to_nuget_20251214

## 目的
Ksql.Linq の `Local → RC → Stable (nuget.org)` フローを参考に、Kafka.Context のリリース手順にも RC 検証と commit hash ロックを組み込む。

## 変更概要
- `docs/workflows/release_roles_and_steps.md` に以下を追加:
  - Release flow（Local → RC → Stable）全体像（mermaid）
  - RC publish（推奨: GitHub Packages）と、CI 未導入時の暫定手順（ローカル feed）
  - 将来の CI 自動化（release-ready signal → auto-tag → nuget publish）の方針

