# diff_manual_commit_api_20251214

## 背景
運用上「成功した時だけ offset を進めたい」ケースに対応するため、manual commit の利用形を固定する。

## 決定事項
- `ForEachAsync(..., autoCommit: false)` の場合、利用者が `EventSet<T>.Commit(entity)` を呼び出して commit できる。
- manual commit は record 単位で、handler 成功後に呼ぶことを推奨する。

## 参照
- `docs/contracts/public_api.md`
- `docs/contracts/retry.md`（commit 粒度）

