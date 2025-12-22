using Kafka.Context.Streaming;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Kafka.Context.Streaming.Flink;

internal sealed class FlinkSqlRenderer
{
    private readonly FlinkExpressionVisitor _expressionVisitor = new(new FlinkFunctionRegistry());

    public string RenderSelect(StreamingQueryPlan plan)
    {
        if (plan.SourceTopics.Count != 0 && plan.SourceTopics.Count != plan.SourceTypes.Count)
            throw new InvalidOperationException("SourceTopics must be aligned to SourceTypes.");

        if (plan.SourceTypes.Count == 0)
            throw new InvalidOperationException("At least one source type is required.");

        if (plan.Window is not null && plan.JoinPredicates.Count > 0)
            throw new InvalidOperationException("Window TVF cannot be combined with JOIN in a single query. Use CTAS first, then JOIN.");

        var sources = plan.SourceTopics.Count == plan.SourceTypes.Count
            ? plan.SourceTopics
            : plan.SourceTypes.Select(t => t.Name).ToArray();

        var fromTable = FlinkSqlIdentifiers.QuoteIdentifier(FlinkSqlIdentifiers.NormalizeIdentifier(sources[0]));
        var sb = new StringBuilder();

        sb.Append("SELECT ");
        sb.Append(RenderSelectList(plan.SelectSelector));
        sb.Append(" FROM ");
        sb.Append(RenderFrom(plan, fromTable));
        sb.Append(" AS t0");

        for (var i = 1; i < sources.Count; i++)
        {
            var joinTable = FlinkSqlIdentifiers.QuoteIdentifier(FlinkSqlIdentifiers.NormalizeIdentifier(sources[i]));
            var joinAlias = "t" + i.ToString(CultureInfo.InvariantCulture);
            sb.Append(" JOIN ");
            sb.Append(joinTable);
            sb.Append(" AS ");
            sb.Append(joinAlias);

            var predicate = plan.JoinPredicates.Count >= i ? plan.JoinPredicates[i - 1] : null;
            if (predicate is not null)
            {
                sb.Append(" ON ");
                sb.Append(RenderPredicate(predicate));
            }
        }

        if (plan.WherePredicates.Count > 0)
        {
            sb.Append(" WHERE ");
            for (var i = 0; i < plan.WherePredicates.Count; i++)
            {
                if (i > 0) sb.Append(" AND ");
                sb.Append(RenderPredicate(plan.WherePredicates[i]));
            }
        }

        if (plan.HasGroupBy && plan.GroupByClause is not null)
        {
            sb.Append(" GROUP BY ");
            var map = FlinkSqlAliasMap.Build(plan.GroupByClause.Parameters);
            var parts = plan.GroupByClause.Keys.Select(k => RenderScalar(k.Expression, map)).ToArray();
            sb.Append(string.Join(", ", parts));
        }

        if (plan.HasHaving && plan.HavingPredicate is not null)
        {
            sb.Append(" HAVING ");
            sb.Append(RenderHaving(plan));
        }

        return sb.ToString();
    }

    private string RenderFrom(StreamingQueryPlan plan, string fromTable)
    {
        if (plan.Window is null)
            return fromTable;

        var timeCol = RenderWindowTimeColumn(plan.Window.TimeColumnName);
        var size = RenderInterval(plan.Window.Size);
        var slide = plan.Window.Slide;

        return plan.Window.Kind switch
        {
            StreamingWindowKind.Tumble =>
                $"TABLE(TUMBLE(TABLE {fromTable}, DESCRIPTOR({timeCol}), {size}))",
            StreamingWindowKind.Hop =>
                $"TABLE(HOP(TABLE {fromTable}, DESCRIPTOR({timeCol}), {RenderInterval(slide ?? throw new InvalidOperationException("HOP requires slide."))}, {size}))",
            StreamingWindowKind.Session =>
                $"TABLE(SESSION(TABLE {fromTable}, DESCRIPTOR({timeCol}), {size}))",
            _ => throw new InvalidOperationException($"Unknown window kind: {plan.Window.Kind}.")
        };
    }

    private static string RenderInterval(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
            throw new InvalidOperationException("Interval must be positive.");

        if (span.TotalDays >= 1 && span.TotalDays % 1 == 0)
            return $"INTERVAL '{(int)span.TotalDays}' DAY";

        if (span.TotalHours >= 1 && span.TotalHours % 1 == 0)
            return $"INTERVAL '{(int)span.TotalHours}' HOUR";

        if (span.TotalMinutes >= 1 && span.TotalMinutes % 1 == 0)
            return $"INTERVAL '{(int)span.TotalMinutes}' MINUTE";

        if (span.TotalSeconds >= 1 && span.TotalSeconds % 1 == 0)
            return $"INTERVAL '{(int)span.TotalSeconds}' SECOND";

        if (span.TotalMilliseconds >= 1 && span.TotalMilliseconds % 1 == 0)
            return $"INTERVAL '{(int)span.TotalMilliseconds}' MILLISECOND";

        throw new InvalidOperationException($"Unsupported interval: {span}.");
    }

    private string RenderSelectList(LambdaExpression? selector)
    {
        var builder = new StreamingSelectClauseBuilder();
        var clause = builder.Build(selector);

        if (clause.IsWildcard)
        {
            if (clause.WildcardParameter is null)
                return "*";

            var aliasMap = FlinkSqlAliasMap.Build(selector!);
            if (aliasMap.TryGetValue(clause.WildcardParameter, out var alias))
                return $"{alias}.*";

            return "*";
        }

        var itemMap = FlinkSqlAliasMap.Build(selector!);
        return string.Join(", ", clause.Items.Select(i =>
        {
            var exprSql = RenderScalar(i.Expression, itemMap);
            var alias = FlinkSqlIdentifiers.QuoteIdentifier(FlinkSqlIdentifiers.NormalizeIdentifier(i.Alias));
            return $"{exprSql} AS {alias}";
        }));
    }

    private string RenderPredicate(StreamingPredicate predicate)
    {
        var map = FlinkSqlAliasMap.Build(predicate.Parameters);
        return RenderScalar(predicate.Body, map);
    }

    private string RenderHaving(StreamingQueryPlan plan)
    {
        var having = plan.HavingPredicate ?? throw new InvalidOperationException("HavingPredicate is required.");
        var selector = plan.SelectSelector ?? throw new InvalidOperationException("SelectSelector is required for HAVING.");

        if (having.Parameters.Count == 1)
        {
            var projection = TryBuildProjectionMap(selector);
            if (projection is not null)
            {
                var rewritten = new ProjectionInliningVisitor(having.Parameters[0], projection).Visit(having.Body);
                if (rewritten is not null && rewritten != having.Body)
                {
                    var map = FlinkSqlAliasMap.Build(selector);
                    return RenderScalar(rewritten, map);
                }
            }
        }

        var fallbackMap = FlinkSqlAliasMap.Build(having);
        return RenderScalar(having.Body, fallbackMap);
    }

    private static Dictionary<string, Expression>? TryBuildProjectionMap(LambdaExpression selector)
    {
        if (selector.Body is MemberInitExpression init)
        {
            var dict = new Dictionary<string, Expression>(StringComparer.Ordinal);
            foreach (var binding in init.Bindings.OfType<MemberAssignment>())
                dict[binding.Member.Name] = binding.Expression;
            return dict;
        }

        if (selector.Body is NewExpression @new && @new.Members is not null && @new.Arguments.Count == @new.Members.Count)
        {
            var dict = new Dictionary<string, Expression>(StringComparer.Ordinal);
            for (var i = 0; i < @new.Members.Count; i++)
                dict[@new.Members[i].Name] = @new.Arguments[i];
            return dict;
        }

        return null;
    }

    private sealed class ProjectionInliningVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly IReadOnlyDictionary<string, Expression> _projectionByMemberName;

        public ProjectionInliningVisitor(ParameterExpression parameter, IReadOnlyDictionary<string, Expression> projectionByMemberName)
        {
            _parameter = parameter;
            _projectionByMemberName = projectionByMemberName;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == _parameter && _projectionByMemberName.TryGetValue(node.Member.Name, out var expr))
                return expr;

            return base.VisitMember(node);
        }
    }

    private string RenderScalar(Expression expression, Dictionary<ParameterExpression, string> paramAliases)
    {
        return _expressionVisitor.Render(expression, paramAliases);
    }

    private string RenderWindowTimeColumn(string timeColumnName)
    {
        if (string.Equals(timeColumnName, FlinkWindow.ProctimeColumnName, StringComparison.Ordinal))
            return "PROCTIME()";

        return FlinkSqlIdentifiers.QuoteIdentifier(FlinkSqlIdentifiers.NormalizeIdentifier(timeColumnName));
    }
}
