namespace Kafka.Context.Streaming;

public sealed record StreamingSinkDefinition(
    Type EntityType,
    string TopicName,
    string ObjectName,
    StreamingSinkMode SinkMode,
    IReadOnlyList<string> PrimaryKeyColumns);

