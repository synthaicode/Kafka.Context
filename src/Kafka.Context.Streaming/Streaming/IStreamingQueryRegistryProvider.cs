namespace Kafka.Context.Streaming;

public interface IStreamingQueryRegistryProvider
{
    IStreamingQueryRegistry StreamingQueries { get; }
}
