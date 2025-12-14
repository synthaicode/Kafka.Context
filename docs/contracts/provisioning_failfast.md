# Contract: Provisioning fail-fast (Kafka.Context)

## 達成できる結果
起動時に「実行前に破綻を防ぐ」ための停止条件（fail fast）と、継続条件を揃えられる。

## 標準方針（移植元: Ksql.Linq 実装）

### 互換性 NG
- Schema 登録時に SR が非互換として拒否した場合、その例外をそのまま伝播して起動を失敗させる（fail fast）。

### subject 不在
- 登録フェーズ:
  - subject の最新取得が 404/40401 の場合は「新規」とみなし、登録で作成する（fail fast しない）。

### 監視/安定化フェーズ（query 出力など）
- subject 待ちが必要なケースは「上限まで待機」し、上限を超えたら起動を失敗させる（fail fast）。
  - 上限（時間/回数）は appsettings で設定可能とする:
    - `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitTimeout`
    - `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitMaxAttempts`
- SR 登録/操作に関するエラー（例: SR 接続不可、認証失敗、5xx 等）は起動を失敗させる（fail fast）。
- schema fullname 不一致は “契約違反” として起動を失敗させる（fail fast）。

## 未解決の論点（次アクション）
- （なし）
