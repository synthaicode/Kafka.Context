# diff_public_api_surface_20251214

## 背景
MVP の利用者体験（Context中心・I/O最小）を維持するため、公開 API の“数”と“形”を先に固定する。

## 決定事項
- `EventSet<T>.ForEachAsync` は以下の overload 群を提供する（simple / headers+meta / timeout / cancellation / autoCommit）。
- `KafkaContext` は `IConfiguration` 直渡しと `sectionName` 指定、ならびに `KafkaContextOptions` を受ける constructor 群を提供する。
- `KafkaContextOptions` は SR client / logger / configuration と初期化/登録に関するフラグを持つ。

## 参照
- `docs/contracts/public_api.md`

