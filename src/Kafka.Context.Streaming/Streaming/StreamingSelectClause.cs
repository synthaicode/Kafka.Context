using System.Collections.Generic;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming;

internal sealed record StreamingSelectClause(
    IReadOnlyList<StreamingSelectItem> Items,
    bool IsWildcard,
    ParameterExpression? WildcardParameter);

internal sealed record StreamingSelectItem(Expression Expression, string Alias);
