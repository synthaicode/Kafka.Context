namespace Kafka.Context.Infrastructure.SchemaRegistry;

public static class SchemaRegistryAvroPreview
{
    public static SchemaRegistryAvroPreviewResult Build(string topicName, Type entityType)
    {
        if (string.IsNullOrWhiteSpace(topicName)) throw new ArgumentException("topicName required", nameof(topicName));
        if (entityType is null) throw new ArgumentNullException(nameof(entityType));

        var keySubject = $"{topicName}-key";
        var valueSubject = $"{topicName}-value";

        var keyPropertyCount = AvroSchemaBuilder.GetKeyPropertyCount(entityType);
        var keySchemaJson = keyPropertyCount >= 2 ? AvroSchemaBuilder.BuildKeySchema(entityType) : null;

        var valueSchemaJson = AvroSchemaBuilder.BuildValueSchema(entityType);
        var expectedValueRecordFullName = AvroSchemaBuilder.GetRecordFullName(valueSchemaJson);

        return new SchemaRegistryAvroPreviewResult(
            topicName,
            entityType.FullName ?? entityType.Name,
            keyPropertyCount,
            keySubject,
            keySchemaJson,
            valueSubject,
            valueSchemaJson,
            expectedValueRecordFullName);
    }
}

public sealed record SchemaRegistryAvroPreviewResult(
    string TopicName,
    string EntityType,
    int KeyPropertyCount,
    string KeySubject,
    string? KeySchemaJson,
    string ValueSubject,
    string ValueSchemaJson,
    string ExpectedValueRecordFullName);

