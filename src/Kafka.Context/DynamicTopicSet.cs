using Avro.Generic;
using Kafka.Context.Diagnostics;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Kafka.Context;

public sealed class DynamicTopicSet
{
    private readonly KafkaContext _context;
    private readonly string _topic;
    private ErrorHandlingPolicy _policy = new();
    private readonly ConcurrentDictionary<MessageMeta, Action> _manualCommit = new();

    internal DynamicTopicSet(KafkaContext context, string topic)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _topic = string.IsNullOrWhiteSpace(topic) ? throw new ArgumentException("topic required", nameof(topic)) : topic;
    }

    public DynamicTopicSet OnError(ErrorAction action)
    {
        _policy = _policy with { ErrorAction = action, RetryEnabled = _policy.RetryEnabled || action == ErrorAction.Retry };
        return this;
    }

    public DynamicTopicSet WithRetry(int maxRetries)
        => WithRetry(maxRetries, TimeSpan.FromSeconds(1));

    public DynamicTopicSet WithRetry(int maxRetries, TimeSpan retryInterval)
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

    public Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action)
        => ForEachAsync(action, autoCommit: true, CancellationToken.None);

    public Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, CancellationToken cancellationToken)
        => ForEachAsync(action, autoCommit: true, cancellationToken);

    public Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return ForEachAsync(action, autoCommit: true, cts.Token);
    }

    public Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit)
        => ForEachAsync(action, autoCommit, CancellationToken.None);

    public Task ForEachAsync(Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action, bool autoCommit, CancellationToken cancellationToken)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        return Kafka.Context.Infrastructure.Runtime.KafkaDynamicConsumerService.ForEachAsync(
            _context.Options,
            _context.LoggerFactory,
            _topic,
            async (key, value, headers, meta) =>
            {
                _context.Logger?.LogInformation(
                    "DynamicTopicSet consumed from {Topic} offset {Offset} timestamp {Timestamp}",
                    meta.Topic,
                    meta.Offset,
                    meta.TimestampUtc);

                KafkaContextMetrics.Consumed("Dynamic", meta.Topic, autoCommit);

                try
                {
                    await ExecuteWithPolicyAsync(key, value, headers, meta, autoCommit, action, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (!autoCommit && _manualCommit.TryRemove(meta, out _))
                    {
                        _context.Logger?.LogWarning(
                            "Manual commit was not called for {Topic} offset {Offset}",
                            meta.Topic,
                            meta.Offset);
                    }
                }
            },
            autoCommit: autoCommit,
            registerCommit: RegisterManualCommit,
            onDecodeError: HandleDecodeErrorAsync,
            cancellationToken);
    }

    public void Commit(MessageMeta meta)
    {
        if (meta is null) throw new ArgumentNullException(nameof(meta));

        if (_manualCommit.TryRemove(meta, out var commit))
        {
            commit();
            KafkaContextMetrics.ManualCommit("Dynamic", meta.Topic);
            return;
        }

        throw new InvalidOperationException("No pending commit found for the specified meta. Commit(meta) is only valid during ForEachAsync(..., autoCommit:false).");
    }

    private void RegisterManualCommit(MessageMeta meta, Action commit)
        => _manualCommit[meta] = commit;

    private async Task ExecuteWithPolicyAsync(
        object? key,
        GenericRecord value,
        Dictionary<string, string> headers,
        MessageMeta meta,
        bool autoCommit,
        Func<object?, GenericRecord, Dictionary<string, string>, MessageMeta, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            var shouldRetry = _policy.RetryEnabled && _policy.RetryCount > 0;
            var attempts = shouldRetry ? _policy.RetryCount + 1 : 1;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    await action(key, value, headers, meta).ConfigureAwait(false);
                    KafkaContextMetrics.HandlerSuccess("Dynamic", meta.Topic, autoCommit);
                    return;
                }
                catch when (attempt < attempts)
                {
                    KafkaContextMetrics.RetryAttempt("Dynamic", meta.Topic, autoCommit);
                    await Task.Delay(_policy.RetryInterval).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            if (_policy.RetryEnabled && _policy.RetryCount > 0)
            {
                KafkaContextMetrics.RetryExhausted("Dynamic", meta.Topic, autoCommit);
                _context.Logger?.LogWarning(
                    "All retry attempts exhausted for {Topic}. MaxAttempts={MaxAttempts} Error={ErrorType}: {Message}",
                    meta.Topic,
                    _policy.RetryCount + 1,
                    ex.GetType().Name,
                    ex.Message);
            }

            KafkaContextMetrics.HandlerError("Dynamic", meta.Topic, autoCommit);
            KafkaContextMetrics.Skipped("Dynamic", meta.Topic, autoCommit, phase: "handler_error");

            _context.Logger?.LogError(
                ex,
                "Handler failed for {Topic}. Error={ErrorType}: {Message}",
                meta.Topic,
                ex.GetType().Name,
                ex.Message);

            if (_policy.ErrorAction == ErrorAction.DLQ)
            {
                await SendToDlqAsync(meta, headers, ex, phase: "handler_error", cancellationToken).ConfigureAwait(false);
            }

            if (!autoCommit)
                Commit(meta);
        }
    }

    private Task HandleDecodeErrorAsync(
        string topic,
        int partition,
        long offset,
        string timestampUtc,
        Dictionary<string, string> headers,
        bool autoCommit,
        Exception ex)
    {
        KafkaContextMetrics.MappingError("Dynamic", topic);
        KafkaContextMetrics.Skipped("Dynamic", topic, autoCommit, phase: "mapping_error");

        _context.Logger?.LogWarning(
            ex,
            "Decode failed on {Topic} {Partition}:{Offset} timestamp {Timestamp}",
            topic,
            partition,
            offset,
            timestampUtc);

        if (_policy.ErrorAction != ErrorAction.DLQ)
            return Task.CompletedTask;

        var meta = new MessageMeta(topic, partition, offset, timestampUtc);
        return SendToDlqAsync(meta, headers, ex, phase: "mapping_error", CancellationToken.None);
    }

    private Task SendToDlqAsync(
        MessageMeta meta,
        Dictionary<string, string> headers,
        Exception ex,
        string phase,
        CancellationToken cancellationToken)
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

        return ProduceDlqAsync(dlqTopicName, env, phase, cancellationToken);
    }

    private async Task ProduceDlqAsync(
        string dlqTopicName,
        Kafka.Context.Messaging.DlqEnvelope env,
        string phase,
        CancellationToken cancellationToken)
    {
        await Kafka.Context.Infrastructure.Runtime.DlqProducerService.ProduceAsync(_context.Options, dlqTopicName, env, cancellationToken).ConfigureAwait(false);
        KafkaContextMetrics.DlqEnqueue("Dynamic", dlqTopicName, phase);
        _context.Logger?.LogInformation(
            "DLQ enqueued to {DlqTopic} phase {Phase}",
            dlqTopicName,
            phase);
    }
}

