# diff_provisioning_failfast_20251214

## 背景
Provisioning の「失敗した場合は起動しない」を実装するため、fail fast の境界を決める。

## 決定事項
- 互換性 NG は SR 登録時に失敗し、そのまま throw（fail fast）とする。
- subject 不在は登録で作成する（fail fast しない）とする。
- 監視/安定化フェーズの subject 待ちは warn のみで継続（fail fast しない）とする。

