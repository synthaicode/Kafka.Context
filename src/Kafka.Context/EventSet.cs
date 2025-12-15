using Kafka.Context.Messaging;
using Kafka.Context.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Kafka.Context;

public sealed class EventSet<T>
{
    private readonly KafkaContext _context;
    private ErrorHandlingPolicy _policy = new();
    private readonly Dictionary<MessageMeta, Action> _manualCommit = new();

    public EventSet(KafkaContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task AddAsync(T entity)
        => AddAsync(entity, CancellationToken.None);

    public Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        return Kafka.Context.Infrastructure.Runtime.KafkaProducerService.ProduceAsync(
            _context.Options,
            _context.LoggerFactory,
            GetTopicName(),
            entity!,
            cancellationToken);
    }

    public Task AddAsync(T entity, Dictionary<string, string> headers)
        => AddAsync(entity, headers, CancellationToken.None);

    public Task AddAsync(T entity, Dictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));

        return Kafka.Context.Infrastructure.Runtime.KafkaProducerService.ProduceAsync(
            _context.Options,
            _context.LoggerFactory,
            GetTopicName(),
            entity!,
            headers,
            cancellationToken);
    }

    public EventSet<T> OnError(ErrorAction action)
    {
        _policy = _policy with { ErrorAction = action, RetryEnabled = _policy.RetryEnabled || action == ErrorAction.Retry };
        return this;
    }

    public EventSet<T> WithRetry(int maxRetries)
    {
        return WithRetry(maxRetries, TimeSpan.FromSeconds(1));
    }

    public EventSet<T> WithRetry(int maxRetries, TimeSpan retryInterval)
    {
        if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
        if (retryInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryInterval));

        _policy = _policy with
        {
            RetryEnabled = true,
            RetryCount = maxRetries,
            RetryInterval = retryInterval
        };

        return this;
    }

    public Task ForEachAsync(Func<T, Task> action)
        => ForEachAsync(action, CancellationToken.None);

    public Task ForEachAsync(Func<T, Task> action, CancellationToken cancellationToken)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        return Kafka.Context.Infrastructure.Runtime.KafkaConsumerService.ForEachAsync<T>(
            _context.Options,
            _context.LoggerFactory,
            GetTopicName(),
            async (entity, headers, meta) =>
            {
                _context.Logger?.LogInformation(
                    "EventSet consumed {EntityType} from {Topic} offset {Offset} timestamp {Timestamp}",
                    typeof(T).Name,
                    GetTopicName(),
                    meta.Offset,
                    meta.TimestampUtc);

                KafkaContextMetrics.Consumed(typeof(T).Name, GetTopicName(), autoCommit: true);
                await ExecuteWithPolicyAsync(entity, headers, meta, autoCommit: true, action).ConfigureAwait(false);
            },
            autoCommit: true,
            registerCommit: (_, _) => { },
            onMappingError: HandleMappingErrorAsync,
            cancellationToken);
    }

    public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return ForEachAsync(action, cts.Token);
    }

    public Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action)
        => ForEachAsync(action, autoCommit: true);

    public Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        return Kafka.Context.Infrastructure.Runtime.KafkaConsumerService.ForEachAsync<T>(
            _context.Options,
            _context.LoggerFactory,
            GetTopicName(),
            async (entity, headers, meta) =>
            {
                _context.Logger?.LogInformation(
                    "EventSet consumed {EntityType} from {Topic} offset {Offset} timestamp {Timestamp}",
                    typeof(T).Name,
                    GetTopicName(),
                    meta.Offset,
                    meta.TimestampUtc);

                KafkaContextMetrics.Consumed(typeof(T).Name, GetTopicName(), autoCommit);
                try
                {
                    await ExecuteWithPolicyAsync(entity, headers, meta, autoCommit, (e) => action(e, headers, meta)).ConfigureAwait(false);
                 }
                 finally
                 {
                    if (!autoCommit && _manualCommit.Remove(meta))
                    {
                        _context.Logger?.LogWarning(
                            "Manual commit was not called for {EntityType} on {Topic} offset {Offset}",
                            typeof(T).Name,
                            GetTopicName(),
                            meta.Offset);
                    }
                }
            },
            autoCommit,
            registerCommit: RegisterManualCommit,
            onMappingError: HandleMappingErrorAsync,
            CancellationToken.None);
    }

    public Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit, CancellationToken cancellationToken)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        return Kafka.Context.Infrastructure.Runtime.KafkaConsumerService.ForEachAsync<T>(
            _context.Options,
            _context.LoggerFactory,
            GetTopicName(),
            async (entity, headers, meta) =>
            {
                _context.Logger?.LogInformation(
                    "EventSet consumed {EntityType} from {Topic} offset {Offset} timestamp {Timestamp}",
                    typeof(T).Name,
                    GetTopicName(),
                    meta.Offset,
                    meta.TimestampUtc);

                KafkaContextMetrics.Consumed(typeof(T).Name, GetTopicName(), autoCommit);
                try
                {
                    await ExecuteWithPolicyAsync(entity, headers, meta, autoCommit, (e) => action(e, headers, meta)).ConfigureAwait(false);
                 }
                 finally
                 {
                    if (!autoCommit && _manualCommit.Remove(meta))
                    {
                        _context.Logger?.LogWarning(
                            "Manual commit was not called for {EntityType} on {Topic} offset {Offset}",
                            typeof(T).Name,
                            GetTopicName(),
                            meta.Offset);
                    }
                }
            },
            autoCommit,
            registerCommit: RegisterManualCommit,
            onMappingError: HandleMappingErrorAsync,
            cancellationToken);
    }

    public Task ForEachAsync(Func<T, Dictionary<string, string>, MessageMeta, Task> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return Kafka.Context.Infrastructure.Runtime.KafkaConsumerService.ForEachAsync<T>(
            _context.Options,
            _context.LoggerFactory,
            GetTopicName(),
            async (entity, headers, meta) =>
            {
                _context.Logger?.LogInformation(
                    "EventSet consumed {EntityType} from {Topic} offset {Offset} timestamp {Timestamp}",
                    typeof(T).Name,
                    GetTopicName(),
                    meta.Offset,
                    meta.TimestampUtc);

                KafkaContextMetrics.Consumed(typeof(T).Name, GetTopicName(), autoCommit: true);
                await ExecuteWithPolicyAsync(entity, headers, meta, autoCommit: true, (e) => action(e, headers, meta)).ConfigureAwait(false);
            },
            autoCommit: true,
            registerCommit: (_, _) => { },
            onMappingError: HandleMappingErrorAsync,
            cts.Token);
    }

    public void Commit(MessageMeta meta)
    {
        if (meta is null) throw new ArgumentNullException(nameof(meta));

        if (_manualCommit.TryGetValue(meta, out var commit))
        {
            commit();
            _manualCommit.Remove(meta);
            KafkaContextMetrics.ManualCommit(typeof(T).Name, GetTopicName());
            return;
        }

        throw new InvalidOperationException("No pending commit found for the specified meta. Commit(meta) is only valid during ForEachAsync(..., autoCommit:false).");
    }

    internal string GetTopicName() => _context.GetTopicNameFor(typeof(T));

    internal ErrorHandlingPolicy Policy => _policy;

    private void RegisterManualCommit(MessageMeta meta, Action commit)
    {
        _manualCommit[meta] = commit;
    }

    private async Task ExecuteWithPolicyAsync(T entity, Dictionary<string, string> headers, MessageMeta meta, bool autoCommit, Func<T, Task> action)
    {
        try
        {
            var shouldRetry = _policy.RetryEnabled && _policy.RetryCount > 0;
            var attempts = shouldRetry ? _policy.RetryCount + 1 : 1;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    await action(entity).ConfigureAwait(false);
                    KafkaContextMetrics.HandlerSuccess(typeof(T).Name, GetTopicName(), autoCommit);
                    return;
                }
                catch when (attempt < attempts)
                {
                    KafkaContextMetrics.RetryAttempt(typeof(T).Name, GetTopicName(), autoCommit);
                    await Task.Delay(_policy.RetryInterval).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            if (_policy.RetryEnabled && _policy.RetryCount > 0)
            {
                KafkaContextMetrics.RetryExhausted(typeof(T).Name, GetTopicName(), autoCommit);
                _context.Logger?.LogWarning(
                    "All retry attempts exhausted for {EntityType} on topic {Topic}. MaxAttempts={MaxAttempts} Error={ErrorType}: {Message}",
                    typeof(T).Name,
                    GetTopicName(),
                    _policy.RetryCount + 1,
                    ex.GetType().Name,
                    ex.Message);
            }

            KafkaContextMetrics.HandlerError(typeof(T).Name, GetTopicName(), autoCommit);
            KafkaContextMetrics.Skipped(typeof(T).Name, GetTopicName(), autoCommit, phase: "handler_error");

            _context.Logger?.LogError(
                ex,
                "Handler failed for {EntityType} on topic {Topic}. Error={ErrorType}: {Message}",
                typeof(T).Name,
                GetTopicName(),
                ex.GetType().Name,
                ex.Message);

            if (_policy.ErrorAction == ErrorAction.DLQ)
            {
                await SendToDlqAsync(meta, headers, ex, phase: "handler_error").ConfigureAwait(false);
            }

            if (!autoCommit)
                Commit(meta);
        }
    }

    private Task HandleMappingErrorAsync(string topic, int partition, long offset, string timestampUtc, Dictionary<string, string> headers, bool autoCommit, Exception ex)
    {
        KafkaContextMetrics.MappingError(typeof(T).Name, topic);
        KafkaContextMetrics.Skipped(typeof(T).Name, topic, autoCommit, phase: "mapping_error");

        _context.Logger?.LogWarning(
            ex,
            "Mapping failed for {EntityType} on {Topic} {Partition}:{Offset} timestamp {Timestamp}",
            typeof(T).Name,
            topic,
            partition,
            offset,
            timestampUtc);

        if (_policy.ErrorAction != ErrorAction.DLQ)
            return Task.CompletedTask;

        var meta = new MessageMeta(topic, partition, offset, timestampUtc);
        return SendToDlqAsync(meta, headers, ex, phase: "mapping_error");
    }

    private Task SendToDlqAsync(MessageMeta meta, Dictionary<string, string> headers, Exception ex, string phase)
    {
        var dlqTopicName = _context.GetDlqTopicName();
        var env = Kafka.Context.Infrastructure.Runtime.DlqEnvelopeFactory.From(
            meta.Topic,
            meta.Partition,
            meta.Offset,
            meta.TimestampUtc,
            headers,
            ex,
            DateTime.UtcNow.ToString("O"));

        return ProduceDlqAsync(dlqTopicName, env, phase);
    }

    private async Task ProduceDlqAsync(string dlqTopicName, Kafka.Context.Messaging.DlqEnvelope env, string phase)
    {
        await Kafka.Context.Infrastructure.Runtime.DlqProducerService.ProduceAsync(_context.Options, dlqTopicName, env, CancellationToken.None).ConfigureAwait(false);
        KafkaContextMetrics.DlqEnqueue(typeof(T).Name, dlqTopicName, phase);
        _context.Logger?.LogInformation(
            "DLQ enqueued {EntityType} to {DlqTopic} phase {Phase}",
            typeof(T).Name,
            dlqTopicName,
            phase);
    }
}
