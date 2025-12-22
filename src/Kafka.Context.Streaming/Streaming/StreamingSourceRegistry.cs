using Kafka.Context.Streaming;
using System;
using System.Collections.Generic;

namespace Kafka.Context.Streaming;

internal sealed class StreamingSourceRegistry : IStreamingSourceRegistry
{
    private readonly Dictionary<Type, StreamingSourceConfig> _configs = new();

    public void AddOrUpdate(Type entityType, StreamingSourceConfig config)
    {
        if (entityType is null) throw new ArgumentNullException(nameof(entityType));
        if (config is null) throw new ArgumentNullException(nameof(config));

        _configs[entityType] = config;
    }

    public bool TryGet(Type entityType, out StreamingSourceConfig config)
    {
        if (entityType is null) throw new ArgumentNullException(nameof(entityType));
        return _configs.TryGetValue(entityType, out config!);
    }
}

