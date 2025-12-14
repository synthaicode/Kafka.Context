namespace Kafka.Context;

public sealed record SchemaRegistryAvroPlan(
    string TopicName,
    string EntityType,
    int KeyPropertyCount,
    string KeySubject,
    string? KeySchemaJson,
    string ValueSubject,
    string ValueSchemaJson,
    string ExpectedValueRecordFullName)
{
    internal static SchemaRegistryAvroPlan From(Kafka.Context.Infrastructure.SchemaRegistry.SchemaRegistryAvroPreviewResult preview)
        => new(
            preview.TopicName,
            preview.EntityType,
            preview.KeyPropertyCount,
            preview.KeySubject,
            preview.KeySchemaJson,
            preview.ValueSubject,
            preview.ValueSchemaJson,
            preview.ExpectedValueRecordFullName);
}

