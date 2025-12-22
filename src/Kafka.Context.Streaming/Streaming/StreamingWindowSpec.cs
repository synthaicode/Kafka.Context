using System;

namespace Kafka.Context.Streaming;

public sealed record StreamingWindowSpec(
    StreamingWindowKind Kind,
    string TimeColumnName,
    TimeSpan Size,
    TimeSpan? Slide);

