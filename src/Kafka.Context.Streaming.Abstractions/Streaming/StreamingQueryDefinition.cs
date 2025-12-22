using System;

namespace Kafka.Context.Streaming;

public sealed record StreamingQueryDefinition(
    Type DerivedType,
    Func<IStreamingQueryBuilder, IStreamingQueryable> QueryFactory,
    StreamingOutputMode OutputMode,
    StreamingSinkMode SinkMode);
