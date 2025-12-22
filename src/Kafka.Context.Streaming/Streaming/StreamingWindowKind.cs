namespace Kafka.Context.Streaming;

public enum StreamingWindowKind
{
    Tumble = 0,
    Hop = 1,
    Session = 2
}
