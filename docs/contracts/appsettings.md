# Contract: appsettings shape (Kafka.Context)

## 達成できる結果
利用者が `appsettings.json` だけで Kafka.Context を起動でき、環境差分は .NET 標準の設定スタックで上書きできる。

English version: `docs/contracts/appsettings.en.md`

## Sources and precedence（override order）
.NET 標準の設定スタックに従い、後勝ちで上書きする:
- Code（builder / Fluent APIs）
- Environment variables（例: `KsqlDsl__...`）
- `appsettings.{Environment}.json`
- `appsettings.json`
- Library defaults

## Minimal configuration（MVP）
```json
{
  "KsqlDsl": {
    "Common": { "BootstrapServers": "127.0.0.1:39092", "ClientId": "my-app" },
    "SchemaRegistry": { "Url": "http://127.0.0.1:18081" },
    "DlqTopicName": "dead_letter_queue",
    "DeserializationErrorPolicy": "DLQ"
  }
}
```

## Key sections
- `KsqlDsl.Common.*`: base Kafka Producer/Consumer options（例: `BootstrapServers`）
- `KsqlDsl.SchemaRegistry.Url`: Schema Registry URL
- `KsqlDsl.DlqTopicName`, `KsqlDsl.DeserializationErrorPolicy`: DLQ settings
- `KsqlDsl.Topics.<name>.Creation.*`: topic creation（partitions/replication/retention, etc.）
- `KsqlDsl.Topics.<name>.Consumer|Producer.*`: per-topic consumer/producer options

## Provisioning / monitoring（MVP）
Schema Registry の subject 待機上限（時間/回数）や監視の挙動は、appsettings で設定可能とする。

- `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitTimeout`: subject 待機の総時間上限（TimeSpan 文字列）
- `KsqlDsl.SchemaRegistry.Monitoring.SubjectWaitMaxAttempts`: subject 待機の試行回数上限（int）

例:
```json
{
  "KsqlDsl": {
    "SchemaRegistry": {
      "Monitoring": {
        "SubjectWaitTimeout": "00:01:00",
        "SubjectWaitMaxAttempts": 12
      }
    }
  }
}
```

## Topic 単位の設定（MVP）
Topic の作成/検証や Producer/Consumer の細かな差分は、`KsqlDsl.Topics.<topicName>.*` に寄せる。

例（topic 作成の partitions を topic 名で指定）:
```json
{
  "KsqlDsl": {
    "Topics": {
      "bar_1m": { "Creation": { "NumPartitions": 3 } }
    }
  }
}
```

補足: topic 情報はモデル側の `KafkaTopicAttribute` でも指定できる（`docs/contracts/topic_attributes.md`）。運用差分は appsettings を優先する。

## Topic Consumer 設定 example
別プロセス起動などで「前後関係」が不確実な場合は、consumer を `AutoOffsetReset = Earliest` に寄せる（未コミット時に先頭から読む）。

```json
{
  "KsqlDsl": {
    "Topics": {
      "orders": {
        "Consumer": {
          "GroupId": "orders-consumer-v1",
          "AutoOffsetReset": "Earliest",
          "AutoCommitIntervalMs": 5000,
          "SessionTimeoutMs": 30000,
          "HeartbeatIntervalMs": 3000,
          "MaxPollIntervalMs": 300000,
          "FetchMinBytes": 1,
          "FetchMaxBytes": 52428800,
          "IsolationLevel": "ReadUncommitted",
          "AdditionalProperties": {
            "enable.partition.eof": "false"
          }
        }
      }
    }
  }
}
```

## Topic Producer 設定 example
producer 側の信頼性/性能要件（idempotence など）は topic 単位で `KsqlDsl.Topics.<name>.Producer.*` に寄せる。

```json
{
  "KsqlDsl": {
    "Common": {
      "BootstrapServers": "localhost:9092",
      "ClientId": "my-producer-app"
    },
    "SchemaRegistry": {
      "Url": "http://localhost:8085"
    },
    "Topics": {
      "orders": {
        "Producer": {
          "Acks": "All",
          "CompressionType": "Snappy",
          "EnableIdempotence": true,
          "MaxInFlightRequestsPerConnection": 1,
          "LingerMs": 5,
          "BatchSize": 16384,
          "BatchNumMessages": 10000,
          "DeliveryTimeoutMs": 120000,
          "RetryBackoffMs": 100,
          "AdditionalProperties": {
            "message.max.bytes": "1000000"
          }
        }
      }
    }
  }
}
```

## Secured Kafka（SASL_SSL）example
```json
{
  "KsqlDsl": {
    "Common": {
      "BootstrapServers": "<broker1>:9092,<broker2>:9092",
      "SecurityProtocol": "SaslSsl",
      "SaslMechanism": "Plain",
      "SaslUsername": "${CLOUD_API_KEY}",
      "SaslPassword": "${CLOUD_API_SECRET}"
    },
    "SchemaRegistry": { "Url": "https://<sr-host>:8081" }
  }
}
```

## 備考
- `KsqlDbUrl` は本 OSS の Non-Goals（ksqlDB engine / query generator）により標準設定から除外する。
