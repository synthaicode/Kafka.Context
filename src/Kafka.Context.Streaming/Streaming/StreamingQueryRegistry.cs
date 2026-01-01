using Kafka.Context.Streaming;
using System;
using System.Collections.Generic;

namespace Kafka.Context.Streaming;

internal sealed class StreamingQueryRegistry : IStreamingQueryRegistry
{
    private readonly object _gate = new();
    private readonly List<StreamingQueryDefinition> _definitions = new();

    public void Add(
        Type derivedType,
        Func<IStreamingQueryBuilder, IStreamingQueryable> queryFactory,
        StreamingOutputMode outputMode,
        StreamingSinkMode sinkMode)
    {
        if (derivedType is null) throw new ArgumentNullException(nameof(derivedType));
        if (queryFactory is null) throw new ArgumentNullException(nameof(queryFactory));

        lock (_gate)
        {
            _definitions.Add(new StreamingQueryDefinition(derivedType, queryFactory, outputMode, sinkMode));
        }
    }

    public IReadOnlyList<StreamingQueryDefinition> GetAll()
    {
        lock (_gate)
        {
            return _definitions.ToArray();
        }
    }
}
