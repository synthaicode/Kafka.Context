# Contract: appsettings shape (Kafka.Context) — English

## Outcome
Users can configure Kafka.Context via `appsettings.json` (and standard .NET configuration overlays), with clear rules for what belongs in `Common`, `Topics.<name>.Consumer`, `Topics.<name>.Producer`, and `Topics.<name>.Creation`.

## Sources & precedence (override order)
Kafka.Context follows the standard .NET configuration stack (last wins):
- Code (builder / in-memory / fluent setup)
- Environment variables (e.g., `KsqlDsl__...`)
- `appsettings.{Environment}.json`
- `appsettings.json`
- Library defaults

## Minimal configuration (MVP)
```json
{
  "KsqlDsl": {
    "Common": { "BootstrapServers": "127.0.0.1:39092", "ClientId": "my-app" },
    "SchemaRegistry": { "Url": "http://127.0.0.1:18081" },
    "DlqTopicName": "dead_letter_queue"
  }
}
```

## What goes where

### `KsqlDsl.Common.*` (global defaults)
Use this for settings that apply to most topics/processes:
- `BootstrapServers`
- `ClientId`
- `AdditionalProperties` (raw Confluent.Kafka config key/value)

### `KsqlDsl.SchemaRegistry.*`
- `Url` (required for provisioning and Avro SerDes)
- `Monitoring.SubjectWaitTimeout` / `Monitoring.SubjectWaitMaxAttempts` (optional)

### `KsqlDsl.Topics.<topicName>.Creation.*` (topic creation/verification)
Use this to control Kafka topic shape:
- `NumPartitions`, `ReplicationFactor`
- `Configs` (e.g., `retention.ms`, `cleanup.policy`)

If the topic already exists, provisioning will compare expected vs actual (fail-fast on mismatch when enforced).

Example:
```json
{
  "KsqlDsl": {
    "Topics": {
      "orders": {
        "Creation": {
          "NumPartitions": 3,
          "ReplicationFactor": 1,
          "Configs": { "retention.ms": "604800000" }
        }
      }
    }
  }
}
```

### `KsqlDsl.Topics.<topicName>.Consumer.*` (per-topic consumer overrides)
Use this when the consumer needs explicit behavior per topic, especially for cross-process scenarios.

Common fields:
- `GroupId`
- `AutoOffsetReset` (`Earliest|Latest|Error`)
- `AutoCommitIntervalMs`, `SessionTimeoutMs`, `HeartbeatIntervalMs`, `MaxPollIntervalMs`
- `FetchMinBytes`, `FetchMaxBytes`
- `IsolationLevel` (`ReadUncommitted|ReadCommitted`)
- `AdditionalProperties` (raw overrides; e.g., `enable.partition.eof`)

Example (cross-process, prefer ordering safety):
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

Note: `EnableAutoCommit` is determined by the API call (`ForEachAsync(..., autoCommit: ...)`) and is not meant to be overridden via appsettings.

### `KsqlDsl.Topics.<topicName>.Producer.*` (per-topic producer overrides)
Use this to tune reliability/performance and keep it explicit per topic.

Common fields:
- `Acks` (`All|Leader|None`)
- `CompressionType` (`None|Gzip|Snappy|Lz4|Zstd`)
- `EnableIdempotence`
- `MaxInFlightRequestsPerConnection`
- `LingerMs`, `BatchSize`, `BatchNumMessages`
- `DeliveryTimeoutMs` (mapped to producer message timeout)
- `RetryBackoffMs`
- `AdditionalProperties` (raw overrides; e.g., `message.max.bytes`)

Example:
```json
{
  "KsqlDsl": {
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

## Key + Schema Registry rule (self-serve)
Single-column key is treated as Kafka primitive and is not registered to SR. Only composite keys are treated as schema contracts.

## Topic name matching note
Per-topic settings are applied by looking up `KsqlDsl.Topics` with:
- exact match (`<topicName>`)
- base topic for `.pub` / `.int`
- (when applicable) prefix fallback (longest-match)

