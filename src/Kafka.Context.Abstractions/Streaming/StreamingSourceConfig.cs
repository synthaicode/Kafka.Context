namespace Kafka.Context.Streaming;

public sealed record StreamingSourceConfig(
    StreamingEventTimeConfig? EventTime);

