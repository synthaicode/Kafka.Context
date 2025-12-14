# diff_provisioning_monitor_boundary_20251214

## 背景
Provisioning の責務を「登録（fail fast）」と「監視（warn 継続）」に分ける場合、停止条件が曖昧だと運用が不安定になる。

## 現状の前提（移植元）
- 登録フェーズ: subject 不在は新規作成（fail fast しない）。
- 監視フェーズ: subject 待ちや schema fullname 不一致は warn のみで継続（fail fast しない）。

## 未解決の論点（次アクション）
- 新ランタイムでも監視フェーズは warn 継続でよいか（停止に寄せるか）。
- schema fullname 不一致を“契約違反”として fail fast に格上げするか。

