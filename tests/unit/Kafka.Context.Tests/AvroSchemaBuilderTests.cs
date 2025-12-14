using Kafka.Context.Attributes;
using System.Reflection;

namespace Kafka.Context.Tests;

public class AvroSchemaBuilderTests
{
    [Fact]
    public void BuildKeySchema_NoKsqlKey_ReturnsStringSchema()
    {
        var builder = GetBuilderType();
        var buildKeySchema = builder.GetMethod("BuildKeySchema", BindingFlags.Public | BindingFlags.Static)!;
        var schema = (string)buildKeySchema.Invoke(null, new object[] { typeof(NoKeyEntity) })!;

        Assert.Equal("\"string\"", schema);
    }

    [Fact]
    public void GetKeyPropertyCount_ReturnsExpectedCounts()
    {
        var builder = GetBuilderType();
        var getCount = builder.GetMethod("GetKeyPropertyCount", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(0, (int)getCount.Invoke(null, new object[] { typeof(NoKeyEntity) })!);
        Assert.Equal(1, (int)getCount.Invoke(null, new object[] { typeof(SingleKeyEntity) })!);
        Assert.Equal(2, (int)getCount.Invoke(null, new object[] { typeof(CompositeKeyEntity) })!);
    }

    [Fact]
    public void BuildValueSchema_DecimalWithoutAttribute_Throws()
    {
        var builder = GetBuilderType();
        var buildValueSchema = builder.GetMethod("BuildValueSchema", BindingFlags.Public | BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() =>
            buildValueSchema.Invoke(null, new object[] { typeof(DecimalEntityWithoutAttr) }));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private static Type GetBuilderType()
    {
        var asm = Assembly.Load("Kafka.Context.Infrastructure");
        return asm.GetType("Kafka.Context.Infrastructure.SchemaRegistry.AvroSchemaBuilder", throwOnError: true)!;
    }

    [KafkaTopic("no-key")]
    private sealed class NoKeyEntity
    {
        public int Id { get; set; }
    }

    [KafkaTopic("single-key")]
    private sealed class SingleKeyEntity
    {
        [KafkaKey] public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [KafkaTopic("composite-key")]
    private sealed class CompositeKeyEntity
    {
        [KafkaKey] public int Id { get; set; }
        [KafkaKey] public long Seq { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [KafkaTopic("decimal-no-attr")]
    private sealed class DecimalEntityWithoutAttr
    {
        public decimal Amount { get; set; }
    }
}
