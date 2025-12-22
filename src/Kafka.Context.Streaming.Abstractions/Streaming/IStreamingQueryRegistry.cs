using System;
using System.Collections.Generic;

namespace Kafka.Context.Streaming;

public interface IStreamingQueryRegistry
{
    void Add(
        Type derivedType,
        Func<IStreamingQueryBuilder, IStreamingQueryable> queryFactory,
        StreamingOutputMode outputMode,
        StreamingSinkMode sinkMode);

    IReadOnlyList<StreamingQueryDefinition> GetAll();
}
