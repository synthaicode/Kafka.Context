using Kafka.Context.Attributes;

namespace Kafka.Context.Messaging;

public sealed class DlqEnvelope
{
    [KsqlKey] public string Topic { get; set; } = string.Empty;
    [KsqlKey] public int Partition { get; set; }
    [KsqlKey] public long Offset { get; set; }

    public string TimestampUtc { get; set; } = string.Empty;
    public string IngestedAtUtc { get; set; } = string.Empty;

    public string PayloadFormatKey { get; set; } = "none";
    public string PayloadFormatValue { get; set; } = "none";
    public string SchemaIdKey { get; set; } = string.Empty;
    public string SchemaIdValue { get; set; } = string.Empty;
    public bool KeyIsNull { get; set; }

    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessageShort { get; set; } = string.Empty;
    public string? StackTraceShort { get; set; }
    public string ErrorFingerprint { get; set; } = string.Empty;

    public string? ApplicationId { get; set; }
    public string? ConsumerGroup { get; set; }
    public string? Host { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();
}

