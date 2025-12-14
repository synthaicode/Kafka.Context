# Contract: DLQ envelope (Kafka.Context)

## 達成できる結果
DLQ に「何が起きたか」「どのレコードか」「再処理に必要な最小情報」を、安定した形で残せる。

## 方針
- DLQ の value は `DlqEnvelope` を標準とする。
- key は `{Topic, Partition, Offset}` の複合キー（再処理で原典を一意に指せる）。
- raw payload は持たない。
- 元トピックのメタ情報（topic/partition/offset・headers・エラー情報等）のみを保持する。
- payload については「形式・SchemaId・null判定」までを保持する（bytes 本体は保持しない）。

## Fingerprint（決定）
- `ErrorMessageShort + "\n" + StackTraceShort` を SHA-256 し、`Convert.ToHexString` の大文字 HEX で `ErrorFingerprint` に保持する。
- `NormalizeStackTraceWhitespace=true` の場合、fingerprint 計算前に `StackTraceShort` の空白（改行・連続空白等）を正規化する。

## TimestampUtc（決定）
- 元レコードの timestamp が取得できない場合、`TimestampUtc` は空文字（`""`）とする。

## 型（目標の形）
```csharp
using Ksql.Linq.Core.Attributes;
using System.Collections.Generic;

namespace Ksql.Linq.Messaging;

public class DlqEnvelope
{
    public DlqEnvelope() { }
    [KsqlKey] public string Topic { get; set; } = string.Empty;
    [KsqlKey] public int Partition { get; set; }
    [KsqlKey] public long Offset { get; set; }

    public string TimestampUtc { get; set; } = string.Empty;  // ISO8601 string
    public string IngestedAtUtc { get; set; } = string.Empty; // ISO8601 string

    public string PayloadFormatKey { get; set; } = "none"; // "avro"/"json"/"protobuf"/"none"
    public string PayloadFormatValue { get; set; } = "none";
    public string SchemaIdKey { get; set; } = string.Empty;
    public string SchemaIdValue { get; set; } = string.Empty;
    public bool KeyIsNull { get; set; }

    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessageShort { get; set; } = string.Empty;
    public string? StackTraceShort { get; set; }
    public string ErrorFingerprint { get; set; } = string.Empty; // SHA-256 of message + stack

    public string? ApplicationId { get; set; }
    public string? ConsumerGroup { get; set; }
    public string? Host { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();
}
```

## 未解決の論点（次アクション）
- （なし）
