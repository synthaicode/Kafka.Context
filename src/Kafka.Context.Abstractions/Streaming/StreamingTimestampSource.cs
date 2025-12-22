namespace Kafka.Context.Streaming;

public enum StreamingTimestampSource
{
    PayloadColumn = 0,
    KafkaRecordTimestamp = 1,
}

