# Contract: appsettings shape (Kafka.Context)

## 達成できる結果
利用者が `appsettings.json` だけで Kafka.Context を起動でき、環境差分は .NET 標準の設定スタックで上書きできる。

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

補足: topic 情報はモデル側の `KsqlTopicAttribute` でも指定できる（`docs/contracts/topic_attributes.md`）。運用差分は appsettings を優先する。

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
