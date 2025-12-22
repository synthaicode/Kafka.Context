namespace Kafka.Context.Streaming;

public interface IStreamingQueryable
{
    StreamingQueryPlan Plan { get; }
}

public interface IStreamingQueryable<T> : IStreamingQueryable { }
public interface IStreamingQueryable<T1, T2> : IStreamingQueryable { }
public interface IStreamingQueryable<T1, T2, T3> : IStreamingQueryable { }
public interface IStreamingQueryable<T1, T2, T3, T4> : IStreamingQueryable { }
