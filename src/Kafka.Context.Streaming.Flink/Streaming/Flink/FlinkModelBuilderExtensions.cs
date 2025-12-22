using Kafka.Context.Abstractions;

namespace Kafka.Context.Streaming.Flink;

public static class FlinkModelBuilderExtensions
{
    public static EntityModelBuilder<T> FlinkSource<T>(
        this EntityModelBuilder<T> builder,
        Action<FlinkSourceBuilder<T>> configure)
        where T : class
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        if (builder.ModelBuilder is not IStreamingSourceRegistryProvider provider)
            throw new InvalidOperationException("The current IModelBuilder does not support streaming source configuration.");

        var b = new FlinkSourceBuilder<T>();
        configure(b);

        provider.StreamingSources.AddOrUpdate(typeof(T), new StreamingSourceConfig(b.EventTime));
        return builder;
    }
}

