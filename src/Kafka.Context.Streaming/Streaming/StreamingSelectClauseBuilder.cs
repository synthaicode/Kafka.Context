using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kafka.Context.Streaming;

internal sealed class StreamingSelectClauseBuilder
{
    public StreamingSelectClause Build(LambdaExpression? selector)
    {
        if (selector is null)
            return new StreamingSelectClause(Array.Empty<StreamingSelectItem>(), IsWildcard: true, WildcardParameter: null);

        if (selector.Body is ParameterExpression p)
            return new StreamingSelectClause(Array.Empty<StreamingSelectItem>(), IsWildcard: true, WildcardParameter: p);

        if (selector.Body is MemberInitExpression init)
            return BuildFromMemberInit(init);

        if (selector.Body is NewExpression @new)
            return BuildFromNew(@new);

        return new StreamingSelectClause(
            new[] { new StreamingSelectItem(selector.Body, "value") },
            IsWildcard: false,
            WildcardParameter: null);
    }

    private static StreamingSelectClause BuildFromMemberInit(MemberInitExpression init)
    {
        var items = new List<StreamingSelectItem>();
        foreach (var binding in init.Bindings.OfType<MemberAssignment>())
        {
            if (binding.Member is not PropertyInfo prop)
                continue;

            items.Add(new StreamingSelectItem(binding.Expression, prop.Name));
        }

        return items.Count == 0
            ? new StreamingSelectClause(Array.Empty<StreamingSelectItem>(), IsWildcard: true, WildcardParameter: null)
            : new StreamingSelectClause(items, IsWildcard: false, WildcardParameter: null);
    }

    private static StreamingSelectClause BuildFromNew(NewExpression expr)
    {
        var items = new List<StreamingSelectItem>();

        if (expr.Members is null || expr.Members.Count != expr.Arguments.Count)
            return new StreamingSelectClause(Array.Empty<StreamingSelectItem>(), IsWildcard: true, WildcardParameter: null);

        for (var i = 0; i < expr.Arguments.Count; i++)
        {
            var member = expr.Members[i];
            items.Add(new StreamingSelectItem(expr.Arguments[i], member.Name));
        }

        return items.Count == 0
            ? new StreamingSelectClause(Array.Empty<StreamingSelectItem>(), IsWildcard: true, WildcardParameter: null)
            : new StreamingSelectClause(items, IsWildcard: false, WildcardParameter: null);
    }
}
