namespace Kafka.Context.Streaming;

public sealed record StreamingSourceDefinition(
    Type EntityType,
    string TopicName,
    string ObjectName,
    StreamingSourceConfig Config);

