# diff_sr_monitor_wait_keys_20251214

## 背景
Provisioning の監視/安定化フェーズで subject 待機上限を appsettings から設定できるようにするため、設定キー名を先に固定する。

## 決定事項
- subject 待機の総時間上限: `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitTimeout`
- subject 待機の試行回数上限: `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitMaxAttempts`

