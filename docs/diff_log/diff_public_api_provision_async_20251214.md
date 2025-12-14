# diff_public_api_provision_async_20251214

## 背景
Provisioning（Kafka topic + Schema Registry）を「起動前に fail fast」させるため、利用者が明示的に呼べる入口を用意する。

## 決定事項
- `KafkaContext` に `ProvisionAsync(CancellationToken)` を公開する。

## 参照
- `docs/contracts/public_api.md`

