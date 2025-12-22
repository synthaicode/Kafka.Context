namespace Kafka.Context.Streaming;

public interface IStreamingSourceRegistry
{
    void AddOrUpdate(Type entityType, StreamingSourceConfig config);
    bool TryGet(Type entityType, out StreamingSourceConfig config);
}

