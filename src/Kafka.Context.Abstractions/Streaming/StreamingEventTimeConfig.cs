namespace Kafka.Context.Streaming;

public sealed record StreamingEventTimeConfig(
    string ColumnName,
    StreamingTimestampSource Source,
    TimeSpan WatermarkDelay);

