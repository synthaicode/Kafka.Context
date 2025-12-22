using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming;

public sealed record StreamingPredicate(
    Expression Body,
    IReadOnlyList<ParameterExpression> Parameters)
{
    public static StreamingPredicate From(LambdaExpression predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return new StreamingPredicate(predicate.Body, predicate.Parameters);
    }
}
