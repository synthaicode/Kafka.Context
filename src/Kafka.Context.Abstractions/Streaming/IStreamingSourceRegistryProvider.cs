namespace Kafka.Context.Streaming;

public interface IStreamingSourceRegistryProvider
{
    IStreamingSourceRegistry StreamingSources { get; }
}

