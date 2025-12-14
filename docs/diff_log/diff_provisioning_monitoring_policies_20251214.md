# diff_provisioning_monitoring_policies_20251214

## 背景
Provisioning の監視/安定化フェーズで「待つ/止める」の境界が曖昧だと、破綻状態のまま起動してしまう。

## 決定事項
- subject 待機には上限（時間/回数）を設け、上限超過は fail fast とする。
  - 上限は appsettings（設定可能）で与える。
- topic 設定不一致（指定された `Creation` と既存 topic が不一致）は fail fast とする。
- schema fullname 不一致は fail fast とする。

