using Confluent.SchemaRegistry;
using Kafka.Context.Configuration;
using Microsoft.Extensions.Logging;

namespace Kafka.Context.Infrastructure.SchemaRegistry;

internal sealed class SchemaProvisioner : IDisposable
{
    private readonly ILogger<SchemaProvisioner>? _logger;
    private readonly SchemaRegistryService _service;

    public SchemaProvisioner(KsqlDslOptions options, ILoggerFactory? loggerFactory = null, ISchemaRegistryClient? client = null)
    {
        _logger = loggerFactory?.CreateLogger<SchemaProvisioner>();
        _service = new SchemaRegistryService(options, loggerFactory, client);
    }

    public async Task ProvisionTopicSchemasAsync(string topicName, Type entityType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(topicName)) throw new ArgumentException("topicName required", nameof(topicName));

        var keySubject = $"{topicName}-key";
        var valueSubject = $"{topicName}-value";

        var valueSchema = AvroSchemaBuilder.BuildValueSchema(entityType);

        var keyCount = AvroSchemaBuilder.GetKeyPropertyCount(entityType);
        if (keyCount >= 2)
        {
            var keySchema = AvroSchemaBuilder.BuildKeySchema(entityType);
            await _service.RegisterOrValidateAsync(keySubject, keySchema, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger?.LogInformation(
                "Skipping Schema Registry registration for key subject {Subject} because key is not composite (keyCount={KeyCount}).",
                keySubject,
                keyCount);
        }

        await _service.RegisterOrValidateAsync(valueSubject, valueSchema, cancellationToken).ConfigureAwait(false);

        var expectedFullName = AvroSchemaBuilder.GetRecordFullName(valueSchema);
        if (!string.IsNullOrWhiteSpace(expectedFullName))
        {
            var actualFullName = await _service.GetLatestRecordFullNameAsync(valueSubject, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(expectedFullName, actualFullName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Schema fullname mismatch for subject '{valueSubject}'. expected='{expectedFullName}', actual='{actualFullName}'");
            }
        }

        _logger?.LogInformation("Schema provisioned: {Topic}", topicName);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _service.Dispose();
    }
}
