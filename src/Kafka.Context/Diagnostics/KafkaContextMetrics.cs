using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kafka.Context.Diagnostics;

internal static class KafkaContextMetrics
{
    private static readonly Meter Meter = new("Kafka.Context", "0.1.0");

    private static readonly Counter<long> ConsumeTotal = Meter.CreateCounter<long>("kafka_context.consume_total");
    private static readonly Counter<long> HandlerSuccessTotal = Meter.CreateCounter<long>("kafka_context.handler_success_total");
    private static readonly Counter<long> HandlerErrorTotal = Meter.CreateCounter<long>("kafka_context.handler_error_total");
    private static readonly Counter<long> MappingErrorTotal = Meter.CreateCounter<long>("kafka_context.mapping_error_total");
    private static readonly Counter<long> RetryAttemptTotal = Meter.CreateCounter<long>("kafka_context.retry_attempt_total");
    private static readonly Counter<long> RetryExhaustedTotal = Meter.CreateCounter<long>("kafka_context.retry_exhausted_total");
    private static readonly Counter<long> DlqEnqueueTotal = Meter.CreateCounter<long>("kafka_context.dlq_enqueue_total");
    private static readonly Counter<long> SkippedTotal = Meter.CreateCounter<long>("kafka_context.skipped_total");
    private static readonly Counter<long> ManualCommitTotal = Meter.CreateCounter<long>("kafka_context.manual_commit_total");

    public static void Consumed(string entity, string topic, bool autoCommit)
        => ConsumeTotal.Add(1, Tags(entity, topic, phase: null, autoCommit));

    public static void HandlerSuccess(string entity, string topic, bool autoCommit)
        => HandlerSuccessTotal.Add(1, Tags(entity, topic, phase: null, autoCommit));

    public static void HandlerError(string entity, string topic, bool autoCommit)
        => HandlerErrorTotal.Add(1, Tags(entity, topic, phase: null, autoCommit));

    public static void MappingError(string entity, string topic)
        => MappingErrorTotal.Add(1, Tags(entity, topic, phase: "mapping_error", autoCommit: null));

    public static void RetryAttempt(string entity, string topic, bool autoCommit)
        => RetryAttemptTotal.Add(1, Tags(entity, topic, phase: "retry", autoCommit));

    public static void RetryExhausted(string entity, string topic, bool autoCommit)
        => RetryExhaustedTotal.Add(1, Tags(entity, topic, phase: "retry", autoCommit));

    public static void DlqEnqueue(string entity, string topic, string phase)
        => DlqEnqueueTotal.Add(1, Tags(entity, topic, phase, autoCommit: null));

    public static void Skipped(string entity, string topic, bool autoCommit, string phase)
        => SkippedTotal.Add(1, Tags(entity, topic, phase, autoCommit));

    public static void ManualCommit(string entity, string topic)
        => ManualCommitTotal.Add(1, Tags(entity, topic, phase: null, autoCommit: false));

    private static TagList Tags(string entity, string topic, string? phase, bool? autoCommit)
    {
        var tags = new TagList
        {
            { "entity", entity },
            { "topic", topic }
        };

        if (!string.IsNullOrWhiteSpace(phase))
            tags.Add("phase", phase);

        if (autoCommit.HasValue)
            tags.Add("auto_commit", autoCommit.Value);

        return tags;
    }
}
