# diff_provisioning_monitor_failfast_on_sr_error_20251214

## 背景
Provisioning の監視/安定化フェーズで SR 側に問題がある場合、warn 継続では破綻状態のまま起動してしまうため、停止条件を明確化する。

## 決定事項
- 監視/安定化フェーズにおいて、Schema Registry の登録/操作に関するエラーが発生した場合は起動を失敗させる（fail fast）。
- subject 不在待ちなど “正常系の待機” は warn 継続とする（別途、待機上限は未確定）。

