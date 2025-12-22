using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kafka.Context.Streaming;

internal sealed class StreamingGroupByClauseBuilder
{
    public StreamingGroupByClause Build(LambdaExpression selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));

        if (selector.Body is MemberInitExpression init)
            return BuildFromMemberInit(init, selector.Parameters);

        if (selector.Body is NewExpression @new)
            return BuildFromNew(@new, selector.Parameters);

        return new StreamingGroupByClause(
            new[] { new StreamingGroupKey(selector.Body, "value") },
            selector.Parameters);
    }

    private static StreamingGroupByClause BuildFromMemberInit(MemberInitExpression init, IReadOnlyList<ParameterExpression> parameters)
    {
        var items = new List<StreamingGroupKey>();
        foreach (var binding in init.Bindings.OfType<MemberAssignment>())
        {
            if (binding.Member is not PropertyInfo prop)
                continue;

            items.Add(new StreamingGroupKey(binding.Expression, prop.Name));
        }

        return new StreamingGroupByClause(items, parameters);
    }

    private static StreamingGroupByClause BuildFromNew(NewExpression expr, IReadOnlyList<ParameterExpression> parameters)
    {
        var items = new List<StreamingGroupKey>();

        if (expr.Members is null || expr.Members.Count != expr.Arguments.Count)
            return new StreamingGroupByClause(items, parameters);

        for (var i = 0; i < expr.Arguments.Count; i++)
        {
            var member = expr.Members[i];
            items.Add(new StreamingGroupKey(expr.Arguments[i], member.Name));
        }

        return new StreamingGroupByClause(items, parameters);
    }
}
