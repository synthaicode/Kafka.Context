namespace Kafka.Context.Streaming;

public interface IStreamingQueryBuilder
{
    IStreamingQueryable<T> From<T>() where T : class;
}
