using System;

namespace Kafka.Context.Abstractions;

public sealed class EntityModelBuilder<T>
{
    public IModelBuilder ModelBuilder { get; }

    public EntityModelBuilder()
        : this(NullModelBuilder.Instance)
    {
    }

    public EntityModelBuilder(IModelBuilder modelBuilder)
    {
        ModelBuilder = modelBuilder ?? throw new ArgumentNullException(nameof(modelBuilder));
    }

    private sealed class NullModelBuilder : IModelBuilder
    {
        public static readonly NullModelBuilder Instance = new();

        private NullModelBuilder() { }

        public EntityModelBuilder<T1> Entity<T1>() => new(this);
    }
}
