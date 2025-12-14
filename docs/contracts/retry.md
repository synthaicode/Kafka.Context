# Contract: Retry (Kafka.Context)

## 達成できる結果
`ForEachAsync` の失敗時挙動（Retry/DLQ/Commit）が予測可能になり、運用が壊れにくくなる。

## 標準方針（移植元: Ksql.Linq 実装）

### 有効条件
- Retry は `OnError=Retry` のときだけ有効。

### 既定値
- 回数既定値: 3

### backoff
- fixed（一定間隔）
- `retryInterval` の既定値: 1s

### retry 対象（例外種別）
- 全例外（`IsRetryable = _ => true`）

### commit のタイミング（要点）
- デシリアライズ/マッピング失敗:
  - （設定が DLQ の場合）DLQ 送信後に即 commit してスキップする。
- ハンドラ例外:
  - `autoCommit=true`（既定）: 明示 commit しない（consumer の auto-commit に任せる）。
  - `autoCommit=false`: （設定が DLQ の場合）DLQ 送信後に commit してスキップする。

## Commit 粒度（決定）
- `autoCommit=false` の場合、commit は record 単位（1 レコード処理ごと）を標準とする。
  - 成功時: handler 完了後に commit
  - 失敗時: DLQ（設定次第）の後に commit してスキップ
- `autoCommit=true`（既定）の場合、Kafka の auto-commit に委譲し、本ライブラリは明示 commit を行わない。

## 未解決の論点（次アクション）
- （なし）
