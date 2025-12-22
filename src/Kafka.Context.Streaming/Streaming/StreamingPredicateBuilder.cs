using System;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming;

internal sealed class StreamingPredicateBuilder
{
    public StreamingPredicate Build(LambdaExpression predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return StreamingPredicate.From(predicate);
    }
}
