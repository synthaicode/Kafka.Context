using Confluent.SchemaRegistry;
using Kafka.Context.Configuration;
using Microsoft.Extensions.Logging;

namespace Kafka.Context.Infrastructure.SchemaRegistry;

internal sealed class SchemaRegistryService : IDisposable
{
    private readonly ILogger<SchemaRegistryService>? _logger;
    private readonly KsqlDslOptions _options;
    private readonly ISchemaRegistryClient _client;
    private bool _disposed;

    public SchemaRegistryService(KsqlDslOptions options, ILoggerFactory? loggerFactory = null, ISchemaRegistryClient? client = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = loggerFactory?.CreateLogger<SchemaRegistryService>();

        if (client is not null)
        {
            _client = client;
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SchemaRegistry.Url))
            throw new InvalidOperationException("KsqlDsl.SchemaRegistry.Url is required.");

        _client = new CachedSchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = _options.SchemaRegistry.Url
        });
    }

    public async Task ValidateConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await _client.GetAllSubjectsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Schema Registry connectivity failed.");
            throw new InvalidOperationException("FATAL: Cannot connect to Schema Registry.", ex);
        }
    }

    public async Task RegisterOrValidateAsync(string subject, string schemaJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("subject is required", nameof(subject));
        if (string.IsNullOrWhiteSpace(schemaJson))
            throw new ArgumentException("schemaJson is required", nameof(schemaJson));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var schema = new Schema(schemaJson, SchemaType.Avro);
            _ = await _client.RegisterSchemaAsync(subject, schema).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Schema Registry registration failed for subject {Subject}", subject);
            throw;
        }
    }

    public async Task<string> GetLatestRecordFullNameAsync(string subject, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var latest = await _client.GetLatestSchemaAsync(subject).ConfigureAwait(false);
            return AvroSchemaBuilder.GetRecordFullName(latest.SchemaString);
        }
        catch (SchemaRegistryException ex) when (ex.ErrorCode is 404 or 40401)
        {
            return string.Empty;
        }
    }

    public async Task WaitForSubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("subject is required", nameof(subject));

        var timeout = _options.SchemaRegistry.Monitoring.SubjectWaitTimeout;
        var maxAttempts = _options.SchemaRegistry.Monitoring.SubjectWaitMaxAttempts;
        if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromSeconds(30);
        if (maxAttempts <= 0) maxAttempts = 10;

        var delay = TimeSpan.FromMilliseconds(Math.Max(100, timeout.TotalMilliseconds / maxAttempts));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cts.Token.ThrowIfCancellationRequested();
            try
            {
                _ = await _client.GetLatestSchemaAsync(subject).ConfigureAwait(false);
                return;
            }
            catch (SchemaRegistryException ex) when (ex.ErrorCode is 404 or 40401)
            {
                _logger?.LogWarning("Subject not found yet: {Subject} (attempt {Attempt}/{MaxAttempts})", subject, attempt, maxAttempts);
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Schema Registry error while waiting subject {Subject}", subject);
                throw new InvalidOperationException($"Schema Registry error while waiting subject '{subject}'", ex);
            }
        }

        throw new TimeoutException($"Subject '{subject}' did not appear within timeout '{timeout}'.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
    }
}
