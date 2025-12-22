namespace Kafka.Context.Streaming.Flink;

public readonly record struct FlinkSqlInterval(int Value, FlinkTimeUnit Unit);
