using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming;

public sealed record StreamingQueryPlan
{
    public StreamingQueryPlan(
        IReadOnlyList<Type> sourceTypes,
        bool hasGroupBy,
        bool hasAggregate,
        bool hasHaving)
    {
        SourceTypes = sourceTypes ?? throw new ArgumentNullException(nameof(sourceTypes));
        HasGroupBy = hasGroupBy;
        HasAggregate = hasAggregate;
        HasHaving = hasHaving;
    }

    public IReadOnlyList<Type> SourceTypes { get; init; }

    /// <summary>
    /// Source topic names, aligned to <see cref="SourceTypes"/>; filled by <c>KafkaContext</c>.
    /// </summary>
    public IReadOnlyList<string> SourceTopics { get; init; } = Array.Empty<string>();

    public StreamingWindowSpec? Window { get; init; }

    public StreamingSinkMode SinkMode { get; init; } = StreamingSinkMode.AppendOnly;

    public IReadOnlyList<StreamingPredicate> JoinPredicates { get; init; } = Array.Empty<StreamingPredicate>();
    public IReadOnlyList<StreamingPredicate> WherePredicates { get; init; } = Array.Empty<StreamingPredicate>();

    public LambdaExpression? SelectSelector { get; init; }
    public LambdaExpression? GroupByKeySelector { get; init; }
    public StreamingGroupByClause? GroupByClause { get; init; }
    public LambdaExpression? HavingPredicate { get; init; }

    public bool HasGroupBy { get; init; }
    public bool HasAggregate { get; init; }
    public bool HasHaving { get; init; }
}
