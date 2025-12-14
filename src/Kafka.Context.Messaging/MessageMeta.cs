namespace Kafka.Context.Messaging;

public sealed record MessageMeta(
    string Topic,
    int Partition,
    long Offset,
    string TimestampUtc);

