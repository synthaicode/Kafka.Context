using System.Collections.Generic;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming;

public sealed record StreamingGroupByClause(
    IReadOnlyList<StreamingGroupKey> Keys,
    IReadOnlyList<ParameterExpression> Parameters);

public sealed record StreamingGroupKey(Expression Expression, string Alias);
