namespace Kafka.Context.Abstractions;

public interface IModelBuilder
{
    EntityModelBuilder<T> Entity<T>();
}
