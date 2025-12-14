# diff_target_code_shape_20251214

## 背景
利用者が「DB屋の感覚」で Kafka + Schema Registry を扱えるようにするため、API の“見た目”を先に固定する。

## 決定事項
- 利用者体験は Context 中心（DbContext ライク）とし、最小 API を以下に寄せる。
  - 書き込み: `AddAsync(key, poco)`（または entity set に対する `AddAsync(poco)`）
  - 読み取り: `ForEachAsync(handler)`
  - 失敗時: `.WithRetry(...)` と `.OnError(...DLQ...)` の連鎖で完結させる
- サンプル（目標の形）を `docs/samples/target_code_shape.md` に固定する。

## 備考
- 名前空間や型名は実装フェーズで `Kafka.Context` に寄せる可能性があるが、利用者視点の形はこのサンプルを基準に維持する。

