using System;
using Kafka.Context.Abstractions;

namespace Kafka.Context.Streaming;

public static class EntityModelBuilderStreamingExtensions
{
    public static EntityModelBuilder<TDerived> ToQuery<TDerived>(
        this EntityModelBuilder<TDerived> builder,
        Func<IStreamingQueryBuilder, IStreamingQueryable> queryFactory,
        StreamingOutputMode outputMode = StreamingOutputMode.Changelog,
        StreamingSinkMode sinkMode = StreamingSinkMode.AppendOnly)
        where TDerived : class
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (queryFactory is null) throw new ArgumentNullException(nameof(queryFactory));

        if (builder.ModelBuilder is not IStreamingQueryRegistryProvider provider)
            throw new InvalidOperationException("The current IModelBuilder does not support streaming queries.");

        provider.StreamingQueries.Add(typeof(TDerived), queryFactory, outputMode, sinkMode);
        return builder;
    }
}
