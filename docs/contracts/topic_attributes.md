# Contract: Topic metadata via attribute (Kafka.Context)

## 達成できる結果
モデル（POCO）に topic 情報を近接させ、Provisioning の入力として利用できる。

## 方針（決定）
- topic 情報は `KafkaTopicAttribute` で設定可能とする。
- attribute は「コード側の既定値」を提供し、運用差分は `KsqlDsl.Topics.<topicName>.Creation.*` で上書きできる。

## 形（目標の形）
```csharp
using System;

namespace Kafka.Context.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class KafkaTopicAttribute : Attribute
{
    public string Name { get; }
    public int PartitionCount { get; set; } = 1;
    public short ReplicationFactor { get; set; } = 1;

    public KafkaTopicAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Topic name cannot be null or empty", nameof(name));
        Name = name;
    }
}
```

## 優先順位（MVP）
1. `KsqlDsl.Topics.<topicName>.Creation.*`（運用で上書き）
2. `KafkaTopicAttribute`（コード側既定）

補足: 互換性のため `KsqlTopicAttribute` などの旧名も残すが、今後は `Kafka*` を推奨する。
