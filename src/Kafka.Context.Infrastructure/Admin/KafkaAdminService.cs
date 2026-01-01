using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Kafka.Context.Configuration;
using Microsoft.Extensions.Logging;

namespace Kafka.Context.Infrastructure.Admin;

internal sealed class KafkaAdminService : IDisposable
{
    private readonly IAdminClient _adminClient;
    private readonly ILogger<KafkaAdminService>? _logger;
    private readonly KsqlDslOptions _options;
    private bool _disposed;

    public KafkaAdminService(KsqlDslOptions options, ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = loggerFactory?.CreateLogger<KafkaAdminService>();

        var adminConfig = CreateAdminConfig(_options);
        _adminClient = new AdminClientBuilder(adminConfig).Build();
    }

    public void ValidateKafkaConnectivity()
    {
        try
        {
            var metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            if (metadata is null || metadata.Brokers.Count == 0)
                throw new InvalidOperationException("No Kafka brokers found in metadata");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "FATAL: Cannot connect to Kafka cluster. Verify bootstrap servers and network connectivity.",
                ex);
        }
    }

    public async Task EnsureDlqTopicExistsAsync(CancellationToken cancellationToken = default)
    {
        var dlqTopicName = string.IsNullOrWhiteSpace(_options.DlqTopicName) ? "dead_letter_queue" : _options.DlqTopicName;
        var expectation = TopicExpectation.ForDlq(dlqTopicName, _options);
        await EnsureTopicExistsAsync(expectation, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureTopicExistsAsync(TopicExpectation expectation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectation.Name))
            throw new ArgumentException("Topic name is required", nameof(expectation));

        var topic = TryGetTopicMetadata(expectation.Name);
        if (topic is null)
        {
            await CreateTopicAsync(expectation, cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("Topic created: {Topic}", expectation.Name);
            return;
        }

        EnsureTopicMatchesExpectation(expectation, topic, cancellationToken);
        _logger?.LogDebug("Topic already exists and matches expectation: {Topic}", expectation.Name);
    }

    public async Task EnsureTopicsExistAsync(IEnumerable<TopicExpectation> expectations, CancellationToken cancellationToken = default)
    {
        foreach (var expectation in expectations)
            await EnsureTopicExistsAsync(expectation, cancellationToken).ConfigureAwait(false);
    }

    public TopicMetadata? TryGetTopicMetadata(string topicName)
    {
        try
        {
            var md = _adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(5));
            var topic = md?.Topics?.FirstOrDefault(t => t.Topic == topicName);
            if (topic is null || topic.Error.IsError)
                return null;
            return topic;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get metadata for {TopicName}", topicName);
            return null;
        }
    }

    private void EnsureTopicMatchesExpectation(TopicExpectation expectation, TopicMetadata topic, CancellationToken cancellationToken)
    {
        if (!expectation.EnforceMatch)
            return;

        if (expectation.NumPartitions > 0 && topic.Partitions.Count != expectation.NumPartitions)
        {
            throw new InvalidOperationException(
                $"Topic '{expectation.Name}' partition count mismatch. expected={expectation.NumPartitions}, actual={topic.Partitions.Count}");
        }

        if (expectation.ReplicationFactor > 0)
        {
            var actualRf = topic.Partitions.Count == 0 ? 0 : topic.Partitions[0].Replicas.Length;
            if (actualRf != expectation.ReplicationFactor)
            {
                throw new InvalidOperationException(
                    $"Topic '{expectation.Name}' replication factor mismatch. expected={expectation.ReplicationFactor}, actual={actualRf}");
            }
        }

        if (expectation.Configs.Count == 0)
            return;

        var actualConfigs = DescribeTopicConfig(expectation.Name, cancellationToken);
        foreach (var kv in expectation.Configs)
        {
            if (!actualConfigs.TryGetValue(kv.Key, out var actual))
                throw new InvalidOperationException($"Topic '{expectation.Name}' config '{kv.Key}' missing. expected='{kv.Value}'");
            if (!string.Equals(actual, kv.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Topic '{expectation.Name}' config '{kv.Key}' mismatch. expected='{kv.Value}', actual='{actual}'");
            }
        }
    }

    private Dictionary<string, string> DescribeTopicConfig(string topicName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resource = new ConfigResource { Type = ResourceType.Topic, Name = topicName };
        try
        {
            var task = _adminClient.DescribeConfigsAsync(new[] { resource });
            task.Wait(cancellationToken);
            var results = task.Result;
            var first = results.FirstOrDefault();
            if (first is null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return first.Entries
                .Where(e => e.Value is not null && e.Value.Value is not null)
                .ToDictionary(e => e.Key, e => e.Value.Value!, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to describe topic config for {Topic}", topicName);
            throw new InvalidOperationException($"Failed to describe topic config for '{topicName}'", ex);
        }
    }

    private async Task CreateTopicAsync(TopicExpectation expectation, CancellationToken cancellationToken)
    {
        var spec = new TopicSpecification
        {
            Name = expectation.Name,
            NumPartitions = expectation.NumPartitions > 0 ? expectation.NumPartitions : 1,
            ReplicationFactor = expectation.ReplicationFactor > 0 ? expectation.ReplicationFactor : (short)1,
            Configs = expectation.Configs.Count == 0 ? null : new Dictionary<string, string>(expectation.Configs)
        };

        if (_options.AdjustReplicationFactorToBrokerCount)
        {
            TryAdjustReplicationFactorToBrokerCount(expectation.Name, ref spec);
        }

        try
        {
            await _adminClient.CreateTopicsAsync(
                new[] { spec },
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30) }).ConfigureAwait(false);
        }
        catch (CreateTopicsException ex)
        {
            var result = ex.Results.FirstOrDefault(r => r.Topic == expectation.Name);
            if (result?.Error.Code == ErrorCode.TopicAlreadyExists)
            {
                _logger?.LogDebug("Topic already exists (race): {Topic}", expectation.Name);
                return;
            }

            if (result?.Error.Code == ErrorCode.InvalidReplicationFactor ||
                (result?.Error.Reason?.IndexOf("Replication factor", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                try
                {
                    spec.ReplicationFactor = 1;
                    _logger?.LogWarning("Retrying creation of {Topic} with ReplicationFactor=1 due to invalid RF.", expectation.Name);
                    await _adminClient.CreateTopicsAsync(
                        new[] { spec },
                        new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30) }).ConfigureAwait(false);
                    return;
                }
                catch (Exception retryEx)
                {
                    _logger?.LogError(retryEx, "Retry with RF=1 failed for topic {Topic}", expectation.Name);
                }
            }

            throw new InvalidOperationException(
                $"Failed to create topic '{expectation.Name}': {result?.Error.Reason ?? "Unknown"}",
                ex);
        }
    }

    private void TryAdjustReplicationFactorToBrokerCount(string topicName, ref TopicSpecification spec)
    {
        try
        {
            var md = _adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            var brokers = md?.Brokers?.Count ?? 0;
            if (brokers > 0 && spec.ReplicationFactor > brokers)
            {
                _logger?.LogWarning(
                    "Requested replication factor {RF} exceeds broker count {Brokers} for {Topic}. Using {AdjRF}.",
                    spec.ReplicationFactor,
                    brokers,
                    topicName,
                    (short)1);
                spec.ReplicationFactor = 1;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get cluster metadata while adjusting RF for {Topic}.", topicName);
        }
    }

    private static AdminClientConfig CreateAdminConfig(KsqlDslOptions options)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = options.Common.BootstrapServers,
            ClientId = $"{options.Common.ClientId}-admin"
        };

        if (options.Common.MetadataMaxAgeMs > 0)
            config.MetadataMaxAgeMs = options.Common.MetadataMaxAgeMs;

        foreach (var kvp in options.Common.AdditionalProperties)
            config.Set(kvp.Key, kvp.Value);

        return config;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _adminClient.Dispose();
        _disposed = true;
    }
}
