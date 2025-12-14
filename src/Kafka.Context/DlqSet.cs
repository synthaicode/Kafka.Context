using Kafka.Context.Diagnostics;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Logging;

namespace Kafka.Context;

public sealed class DlqSet
{
    private readonly KafkaContext _context;

    internal DlqSet(KafkaContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task ForEachAsync(Func<DlqEnvelope, Task> action)
        => ForEachAsync(action, CancellationToken.None);

    public Task ForEachAsync(Func<DlqEnvelope, Task> action, CancellationToken cancellationToken)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        return Kafka.Context.Infrastructure.Runtime.KafkaConsumerService.ForEachAsync<DlqEnvelope>(
            _context.Options,
            _context.LoggerFactory,
            _context.GetDlqTopicName(),
            async (entity, headers, meta) =>
            {
                _context.Logger?.LogInformation(
                    "Dlq consumed {Topic} offset {Offset} timestamp {Timestamp}",
                    meta.Topic,
                    meta.Offset,
                    meta.TimestampUtc);

                KafkaContextMetrics.Consumed(nameof(DlqEnvelope), meta.Topic, autoCommit: true);
                await action(entity).ConfigureAwait(false);
                KafkaContextMetrics.HandlerSuccess(nameof(DlqEnvelope), meta.Topic, autoCommit: true);
            },
            autoCommit: true,
            registerCommit: (_, _) => { },
            onMappingError: HandleMappingErrorAsync,
            cancellationToken);
    }

    public Task ForEachAsync(Func<DlqEnvelope, Task> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return ForEachAsync(action, cts.Token);
    }

    public Task ForEachAsync(Func<DlqEnvelope, Dictionary<string, string>, MessageMeta, Task> action)
        => ForEachAsync(action, CancellationToken.None);

    public Task ForEachAsync(Func<DlqEnvelope, Dictionary<string, string>, MessageMeta, Task> action, CancellationToken cancellationToken)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        return Kafka.Context.Infrastructure.Runtime.KafkaConsumerService.ForEachAsync<DlqEnvelope>(
            _context.Options,
            _context.LoggerFactory,
            _context.GetDlqTopicName(),
            async (entity, headers, meta) =>
            {
                _context.Logger?.LogInformation(
                    "Dlq consumed {Topic} offset {Offset} timestamp {Timestamp}",
                    meta.Topic,
                    meta.Offset,
                    meta.TimestampUtc);

                KafkaContextMetrics.Consumed(nameof(DlqEnvelope), meta.Topic, autoCommit: true);
                await action(entity, headers, meta).ConfigureAwait(false);
                KafkaContextMetrics.HandlerSuccess(nameof(DlqEnvelope), meta.Topic, autoCommit: true);
            },
            autoCommit: true,
            registerCommit: (_, _) => { },
            onMappingError: HandleMappingErrorAsync,
            cancellationToken);
    }

    public Task ForEachAsync(Func<DlqEnvelope, Dictionary<string, string>, MessageMeta, Task> action, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return ForEachAsync(action, cts.Token);
    }

    private Task HandleMappingErrorAsync(string topic, int partition, long offset, string timestampUtc, Dictionary<string, string> headers, bool autoCommit, Exception ex)
    {
        KafkaContextMetrics.MappingError(nameof(DlqEnvelope), topic);
        KafkaContextMetrics.Skipped(nameof(DlqEnvelope), topic, autoCommit, phase: "mapping_error");

        _context.Logger?.LogWarning(
            ex,
            "Dlq mapping failed for {Topic} {Partition}:{Offset} timestamp {Timestamp}",
            topic,
            partition,
            offset,
            timestampUtc);

        return Task.CompletedTask;
    }
}
