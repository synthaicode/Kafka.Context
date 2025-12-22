using Kafka.Context.Streaming;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Kafka.Context.Streaming.Flink;

internal sealed class FlinkSqlRenderer
{
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "values",
        "coalesce",
    };

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

        var fromTable = QuoteIdentifier(NormalizeIdentifier(sources[0]));
        var sb = new StringBuilder();

        sb.Append("SELECT ");
        sb.Append(RenderSelectList(plan.SelectSelector));
        sb.Append(" FROM ");
        sb.Append(RenderFrom(plan, fromTable));
        sb.Append(" AS t0");

        for (var i = 1; i < sources.Count; i++)
        {
            var joinTable = QuoteIdentifier(NormalizeIdentifier(sources[i]));
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

        if (plan.HasGroupBy && plan.GroupByKeySelector is not null)
        {
            var map = BuildParameterAliasMap(plan.GroupByKeySelector);
            sb.Append(" GROUP BY ");
            sb.Append(RenderScalar(plan.GroupByKeySelector.Body, map));
        }

        if (plan.HasHaving && plan.HavingPredicate is not null)
        {
            sb.Append(" HAVING ");
            sb.Append(RenderHaving(plan));
        }

        return sb.ToString();
    }

    public string QuoteIdentifier(string identifier)
    {
        var escaped = identifier.Replace("`", "``", StringComparison.Ordinal);
        return $"`{escaped}`";
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

    private static string NormalizeIdentifier(string suggested)
    {
        if (string.IsNullOrWhiteSpace(suggested))
            return "t";

        var normalized = suggested.Trim().Replace('-', '_');
        if (ReservedWords.Contains(normalized))
            normalized = "t_" + normalized;

        return normalized;
    }

    private string RenderSelectList(LambdaExpression? selector)
    {
        if (selector is null)
            return "*";

        var map = BuildParameterAliasMap(selector);

        if (selector.Body is MemberInitExpression init)
        {
            var parts = new List<string>(init.Bindings.Count);
            foreach (var binding in init.Bindings.OfType<MemberAssignment>())
            {
                if (binding.Member is not PropertyInfo prop)
                    continue;

                var exprSql = RenderScalar(binding.Expression, map);
                var alias = QuoteIdentifier(NormalizeIdentifier(prop.Name));
                parts.Add($"{exprSql} AS {alias}");
            }

            return parts.Count == 0 ? "*" : string.Join(", ", parts);
        }

        if (selector.Body is NewExpression @new && @new.Members is not null && @new.Arguments.Count == @new.Members.Count)
        {
            var parts = new List<string>(@new.Arguments.Count);
            for (var i = 0; i < @new.Arguments.Count; i++)
            {
                var member = @new.Members[i];
                var exprSql = RenderScalar(@new.Arguments[i], map);
                var alias = QuoteIdentifier(NormalizeIdentifier(member.Name));
                parts.Add($"{exprSql} AS {alias}");
            }

            return parts.Count == 0 ? "*" : string.Join(", ", parts);
        }

        if (selector.Body is ParameterExpression p && map.TryGetValue(p, out var aliasName))
            return $"{aliasName}.*";

        return "*";
    }

    private string RenderPredicate(LambdaExpression predicate)
    {
        var map = BuildParameterAliasMap(predicate);
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
                    var map = BuildParameterAliasMap(selector);
                    return RenderScalar(rewritten, map);
                }
            }
        }

        var fallbackMap = BuildParameterAliasMap(having);
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

    private static Dictionary<ParameterExpression, string> BuildParameterAliasMap(LambdaExpression lambda)
    {
        var map = new Dictionary<ParameterExpression, string>();
        for (var i = 0; i < lambda.Parameters.Count; i++)
        {
            map[lambda.Parameters[i]] = "t" + i.ToString(CultureInfo.InvariantCulture);
        }
        return map;
    }

    private string RenderScalar(Expression expression, Dictionary<ParameterExpression, string> paramAliases)
    {
        return expression.NodeType switch
        {
            ExpressionType.Constant => RenderConstant((ConstantExpression)expression),
            ExpressionType.MemberAccess => RenderMemberAccess((MemberExpression)expression, paramAliases),
            ExpressionType.Convert or ExpressionType.ConvertChecked => RenderScalar(((UnaryExpression)expression).Operand, paramAliases),
            ExpressionType.New => RenderNew((NewExpression)expression, paramAliases),
            ExpressionType.MemberInit => RenderMemberInit((MemberInitExpression)expression, paramAliases),
            ExpressionType.Not => $"(NOT {RenderScalar(((UnaryExpression)expression).Operand, paramAliases)})",
            ExpressionType.Conditional => RenderConditional((ConditionalExpression)expression, paramAliases),
            ExpressionType.AndAlso => RenderBinary((BinaryExpression)expression, "AND", paramAliases),
            ExpressionType.OrElse => RenderBinary((BinaryExpression)expression, "OR", paramAliases),
            ExpressionType.Equal => RenderBinary((BinaryExpression)expression, "=", paramAliases),
            ExpressionType.NotEqual => RenderBinary((BinaryExpression)expression, "<>", paramAliases),
            ExpressionType.GreaterThan => RenderBinary((BinaryExpression)expression, ">", paramAliases),
            ExpressionType.GreaterThanOrEqual => RenderBinary((BinaryExpression)expression, ">=", paramAliases),
            ExpressionType.LessThan => RenderBinary((BinaryExpression)expression, "<", paramAliases),
            ExpressionType.LessThanOrEqual => RenderBinary((BinaryExpression)expression, "<=", paramAliases),
            ExpressionType.Add => RenderBinary((BinaryExpression)expression, "+", paramAliases),
            ExpressionType.Subtract => RenderBinary((BinaryExpression)expression, "-", paramAliases),
            ExpressionType.Multiply => RenderBinary((BinaryExpression)expression, "*", paramAliases),
            ExpressionType.Divide => RenderBinary((BinaryExpression)expression, "/", paramAliases),
            ExpressionType.Coalesce => RenderCoalesce((BinaryExpression)expression, paramAliases),
            ExpressionType.Call => RenderCall((MethodCallExpression)expression, paramAliases),
            _ => throw new NotSupportedException($"Unsupported expression node: {expression.NodeType} ({expression.GetType().Name}).")
        };
    }

    private string RenderNew(NewExpression expr, Dictionary<ParameterExpression, string> map)
    {
        var parts = expr.Arguments.Select(a => RenderScalar(a, map)).ToArray();
        return string.Join(", ", parts);
    }

    private string RenderMemberInit(MemberInitExpression expr, Dictionary<ParameterExpression, string> map)
    {
        var parts = expr.Bindings
            .OfType<MemberAssignment>()
            .Select(b => RenderScalar(b.Expression, map))
            .ToArray();

        return string.Join(", ", parts);
    }

    private string RenderBinary(BinaryExpression binary, string op, Dictionary<ParameterExpression, string> map)
    {
        return $"({RenderScalar(binary.Left, map)} {op} {RenderScalar(binary.Right, map)})";
    }

    private string RenderCoalesce(BinaryExpression binary, Dictionary<ParameterExpression, string> map)
    {
        return $"COALESCE({RenderScalar(binary.Left, map)}, {RenderScalar(binary.Right, map)})";
    }

    private string RenderConstant(ConstantExpression c)
    {
        if (c.Value is null) return "NULL";

        return c.Value switch
        {
            string s => $"'{s.Replace("'", "''", StringComparison.Ordinal)}'",
            bool b => b ? "TRUE" : "FALSE",
            int or long or short or byte or sbyte or uint or ulong or ushort =>
                Convert.ToString(c.Value, CultureInfo.InvariantCulture)!,
            float or double or decimal =>
                Convert.ToString(c.Value, CultureInfo.InvariantCulture)!,
            DateTime dt => $"TIMESTAMP '{dt:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset dto => $"TIMESTAMP '{dto:yyyy-MM-dd HH:mm:ss}'",
            _ => throw new NotSupportedException($"Unsupported constant type: {c.Type.FullName}.")
        };
    }

    private string RenderMemberAccess(MemberExpression m, Dictionary<ParameterExpression, string> map)
    {
        if (m.Expression is ParameterExpression p && map.TryGetValue(p, out var alias))
        {
            var col = QuoteIdentifier(NormalizeIdentifier(m.Member.Name));
            return $"{alias}.{col}";
        }

        if (m.Member.DeclaringType == typeof(string) && m.Member.Name == nameof(string.Length))
            return $"CHAR_LENGTH({RenderScalar(m.Expression!, map)})";

        if ((m.Member.DeclaringType == typeof(DateTime) || m.Member.DeclaringType == typeof(DateTimeOffset)) &&
            m.Member is PropertyInfo pi)
        {
            var part = pi.Name switch
            {
                nameof(DateTime.Year) => "YEAR",
                nameof(DateTime.Month) => "MONTH",
                nameof(DateTime.Day) => "DAY",
                nameof(DateTime.Hour) => "HOUR",
                nameof(DateTime.Minute) => "MINUTE",
                nameof(DateTime.Second) => "SECOND",
                _ => null
            };

            if (part is not null)
                return $"EXTRACT({part} FROM {RenderScalar(m.Expression!, map)})";
        }

        throw new NotSupportedException($"Unsupported member access: {m.Member.DeclaringType?.FullName}.{m.Member.Name}.");
    }

    private string RenderCall(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        if (call.Method.DeclaringType == typeof(string))
            return RenderStringCall(call, map);

        if (call.Method.DeclaringType == typeof(Math))
            return RenderMathCall(call, map);

        if (call.Method.DeclaringType == typeof(FlinkSql))
            return RenderFlinkSqlCall(call, map);

        if (call.Method.DeclaringType == typeof(FlinkWindow))
            return RenderWindowCall(call);

        if (call.Method.DeclaringType == typeof(FlinkAgg))
            return RenderAggCall(call, map);

        throw new NotSupportedException($"Unsupported method call: {call.Method.DeclaringType?.FullName}.{call.Method.Name}.");
    }

    private string RenderWindowCall(MethodCallExpression call)
    {
        return call.Method.Name switch
        {
            nameof(FlinkWindow.Start) => "window_start",
            nameof(FlinkWindow.End) => "window_end",
            nameof(FlinkWindow.Proctime) => "PROCTIME()",
            _ => throw new NotSupportedException($"Unsupported FlinkWindow method: {call.Method.Name}.")
        };
    }

    private string RenderAggCall(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        return call.Method.Name switch
        {
            nameof(FlinkAgg.Count) when call.Arguments.Count == 0 => "COUNT(*)",
            nameof(FlinkAgg.Sum) when call.Arguments.Count == 1 => $"SUM({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkAgg.Avg) when call.Arguments.Count == 1 => $"AVG({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkAgg.EarliestByOffset) => throw new NotSupportedException("EARLIEST_BY_OFFSET is not supported in Flink SQL."),
            nameof(FlinkAgg.LatestByOffset) => throw new NotSupportedException("LATEST_BY_OFFSET is not supported in Flink SQL."),
            _ => throw new NotSupportedException($"Unsupported FlinkAgg method: {call.Method.Name}.")
        };
    }

    private string RenderStringCall(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        var instance = call.Object is null ? null : RenderScalar(call.Object, map);

        return call.Method.Name switch
        {
            nameof(string.Substring) => RenderSubstring(call, map),
            nameof(string.Replace) => $"REPLACE({instance}, {RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(string.Trim) => $"TRIM({instance})",
            nameof(string.ToUpper) => $"UPPER({instance})",
            nameof(string.ToLower) => $"LOWER({instance})",
            nameof(string.IndexOf) => $"(POSITION({RenderScalar(call.Arguments[0], map)} IN {instance}) - 1)",
            nameof(string.Contains) => RenderLike(instance!, call.Arguments[0], map, LikeMode.Contains),
            nameof(string.StartsWith) => RenderLike(instance!, call.Arguments[0], map, LikeMode.StartsWith),
            nameof(string.EndsWith) => RenderLike(instance!, call.Arguments[0], map, LikeMode.EndsWith),
            nameof(string.Split) => throw new NotSupportedException("string.Split is not supported in Flink SQL (use SPLIT_INDEX for index-based extraction)."),
            _ => throw new NotSupportedException($"Unsupported string method: {call.Method.Name}.")
        };
    }

    private string RenderConditional(ConditionalExpression expression, Dictionary<ParameterExpression, string> map)
    {
        var test = RenderScalar(expression.Test, map);
        var ifTrue = RenderScalar(expression.IfTrue, map);
        var ifFalse = RenderScalar(expression.IfFalse, map);
        return $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";
    }

    private string RenderFlinkSqlCall(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        var name = call.Method.Name;
        return name switch
        {
            nameof(FlinkSql.Concat) => $"CONCAT({RenderParams(call, map)})",
            nameof(FlinkSql.Locate) => $"LOCATE({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.Position) => $"POSITION({RenderScalar(call.Arguments[0], map)} IN {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.Lpad) => $"LPAD({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)}, {RenderScalar(call.Arguments[2], map)})",
            nameof(FlinkSql.Rpad) => $"RPAD({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)}, {RenderScalar(call.Arguments[2], map)})",
            nameof(FlinkSql.SplitIndex) => $"SPLIT_INDEX({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)}, {RenderScalar(call.Arguments[2], map)})",
            nameof(FlinkSql.RegexpExtract) => $"REGEXP_EXTRACT({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)}, {RenderScalar(call.Arguments[2], map)})",
            nameof(FlinkSql.RegexpReplace) => $"REGEXP_REPLACE({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)}, {RenderScalar(call.Arguments[2], map)})",
            nameof(FlinkSql.SimilarTo) => $"{RenderScalar(call.Arguments[0], map)} SIMILAR TO {RenderScalar(call.Arguments[1], map)}",
            nameof(FlinkSql.Regexp) => $"{RenderScalar(call.Arguments[0], map)} REGEXP {RenderScalar(call.Arguments[1], map)}",
            nameof(FlinkSql.JsonValue) => $"JSON_VALUE({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.JsonQuery) => $"JSON_QUERY({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.JsonExists) => $"JSON_EXISTS({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.JsonLength) => throw new NotSupportedException("JSON_LENGTH is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonArrayLength) => throw new NotSupportedException("JSON_ARRAY_LENGTH is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonKeys) => throw new NotSupportedException("JSON_KEYS is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonObjectKeys) => throw new NotSupportedException("JSON_OBJECT_KEYS is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonTable) => throw new NotSupportedException("JSON_TABLE is not supported in this Flink dialect."),
            nameof(FlinkSql.Cast) => $"CAST({RenderScalar(call.Arguments[0], map)} AS {RenderType(call.Arguments[1])})",
            nameof(FlinkSql.TryCast) => $"TRY_CAST({RenderScalar(call.Arguments[0], map)} AS {RenderType(call.Arguments[1])})",
            nameof(FlinkSql.ToTimestamp) when call.Arguments.Count == 1 => $"TO_TIMESTAMP({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.ToTimestamp) when call.Arguments.Count == 2 => $"TO_TIMESTAMP({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.ToTimestampLtz) => $"TO_TIMESTAMP_LTZ({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.FromUnixtime) => $"FROM_UNIXTIME({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.CurrentTimestamp) => "CURRENT_TIMESTAMP",
            nameof(FlinkSql.DateFormat) => $"DATE_FORMAT({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.Extract) => $"EXTRACT({RenderTimeUnit(call.Arguments[0])} FROM {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.TimestampAdd) => $"TIMESTAMPADD({RenderTimeUnit(call.Arguments[0])}, {RenderScalar(call.Arguments[1], map)}, {RenderScalar(call.Arguments[2], map)})",
            nameof(FlinkSql.TimestampDiff) => $"TIMESTAMPDIFF({RenderTimeUnit(call.Arguments[0])}, {RenderScalar(call.Arguments[1], map)}, {RenderScalar(call.Arguments[2], map)})",
            nameof(FlinkSql.DayOfWeek) => $"DAYOFWEEK({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.DayOfYear) => $"DAYOFYEAR({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.WeekOfYearExtract) => $"EXTRACT(WEEK FROM {RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.WeekOfYearFormat) => $"DATE_FORMAT({RenderScalar(call.Arguments[0], map)}, 'w')",
            nameof(FlinkSql.FloorTo) => $"FLOOR({RenderScalar(call.Arguments[0], map)} TO {RenderTimeUnit(call.Arguments[1])})",
            nameof(FlinkSql.Interval) => RenderIntervalLiteral(call.Arguments[0], call.Arguments[1]),
            nameof(FlinkSql.DateLiteral) => $"DATE {RenderScalar(call.Arguments[0], map)}",
            nameof(FlinkSql.AddInterval) => $"{RenderScalar(call.Arguments[0], map)} + {RenderIntervalExpression(call.Arguments[1], map)}",
            nameof(FlinkSql.SubInterval) => $"{RenderScalar(call.Arguments[0], map)} - {RenderIntervalExpression(call.Arguments[1], map)}",
            nameof(FlinkSql.ToUtcTimestamp) => $"TO_UTC_TIMESTAMP({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.FromUtcTimestamp) => $"FROM_UTC_TIMESTAMP({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.Uuid) => "UUID()",
            nameof(FlinkSql.IfNull) => $"IFNULL({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.NullIf) => $"NULLIF({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.Like) => RenderLikePredicate(call, map),
            nameof(FlinkSql.Array) => $"ARRAY[{RenderParams(call, map)}]",
            nameof(FlinkSql.ArrayLength) => $"CARDINALITY({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.ArrayElement) => RenderArrayElement(call, map),
            nameof(FlinkSql.ArrayContains) => $"ARRAY_CONTAINS({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.ArrayDistinct) => $"ARRAY_DISTINCT({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.ArrayJoin) => $"ARRAY_JOIN({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(FlinkSql.Map) => $"MAP[{RenderParams(call, map)}]",
            nameof(FlinkSql.MapKeys) => $"MAP_KEYS({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.MapValues) => $"MAP_VALUES({RenderScalar(call.Arguments[0], map)})",
            nameof(FlinkSql.MapGet) => $"{RenderScalar(call.Arguments[0], map)}[{RenderScalar(call.Arguments[1], map)}]",
            nameof(FlinkSql.OverRows) => throw new NotSupportedException("OVER windows are not supported in this Flink dialect."),
            nameof(FlinkSql.OverRangeInterval) => throw new NotSupportedException("OVER windows are not supported in this Flink dialect."),
            nameof(FlinkSql.OverRangeNumeric) => throw new NotSupportedException("OVER windows are not supported in this Flink dialect."),
            nameof(FlinkSql.KsqlInstr) => throw new NotSupportedException("INSTR is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlLen) => throw new NotSupportedException("LEN is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlTimestampToString) => throw new NotSupportedException("TIMESTAMPTOSTRING is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlDateAdd) => throw new NotSupportedException("DATEADD is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlJsonExtractString) => throw new NotSupportedException("JSON_EXTRACT_STRING is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlUcase) => throw new NotSupportedException("UCASE is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlLcase) => throw new NotSupportedException("LCASE is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlFormatTimestamp) => throw new NotSupportedException("FORMAT_TIMESTAMP is not supported in Flink SQL."),
            nameof(FlinkSql.KsqlJsonArrayLength) => throw new NotSupportedException("JSON_ARRAY_LENGTH is not supported in Flink SQL."),
            nameof(FlinkSql.Nvl) => throw new NotSupportedException("NVL is not supported in Flink SQL."),
            _ => throw new NotSupportedException($"Unsupported FlinkSql method: {name}.")
        };
    }

    private string RenderLikePredicate(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        var value = RenderScalar(call.Arguments[0], map);
        var pattern = RenderScalar(call.Arguments[1], map);
        if (call.Arguments.Count < 3)
            return $"{value} LIKE {pattern}";

        if (call.Arguments[2] is ConstantExpression ce && ce.Value is null)
            return $"{value} LIKE {pattern}";

        return $"{value} LIKE {pattern} ESCAPE {RenderScalar(call.Arguments[2], map)}";
    }

    private string RenderArrayElement(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        if (call.Arguments[1] is ConstantExpression ce && ce.Value is int i && i <= 0)
            throw new NotSupportedException("Array index must be 1-based in Flink SQL.");

        return $"{RenderScalar(call.Arguments[0], map)}[{RenderScalar(call.Arguments[1], map)}]";
    }

    private static string RenderParams(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        if (call.Arguments.Count == 1 && call.Arguments[0] is NewArrayExpression arr)
            return string.Join(", ", arr.Expressions.Select(e => RenderStatic(e, map)));

        return string.Join(", ", call.Arguments.Select(a => RenderStatic(a, map)));
    }

    private static string RenderStatic(Expression expr, Dictionary<ParameterExpression, string> map)
    {
        var renderer = new FlinkSqlRenderer();
        return renderer.RenderScalar(expr, map);
    }

    private static string RenderType(Expression expr)
    {
        if (expr is ConstantExpression ce && ce.Value is FlinkSqlType type)
            return type.ToSql();

        if (expr is MemberExpression me && me.Member is System.Reflection.PropertyInfo pi && pi.DeclaringType == typeof(FlinkSqlType))
        {
            var value = pi.GetValue(null);
            if (value is FlinkSqlType t)
                return t.ToSql();
        }

        if (expr is MethodCallExpression call && call.Method.DeclaringType == typeof(FlinkSqlType))
        {
            if (call.Method.Name == nameof(FlinkSqlType.Decimal) && call.Arguments.Count == 2)
            {
                if (call.Arguments[0] is ConstantExpression p && p.Value is int precision &&
                    call.Arguments[1] is ConstantExpression s && s.Value is int scale)
                    return FlinkSqlType.Decimal(precision, scale).ToSql();
            }

            if (call.Method.Name == nameof(FlinkSqlType.TimestampLtz) && call.Arguments.Count == 1)
            {
                if (call.Arguments[0] is ConstantExpression pr && pr.Value is int precision)
                    return FlinkSqlType.TimestampLtz(precision).ToSql();
            }
        }

        throw new NotSupportedException("FlinkSqlType must be a constant or static type helper.");
    }

    private static string RenderTimeUnit(Expression expr)
    {
        if (expr is ConstantExpression ce && ce.Value is FlinkTimeUnit unit)
        {
            return unit switch
            {
                FlinkTimeUnit.Year => "YEAR",
                FlinkTimeUnit.Month => "MONTH",
                FlinkTimeUnit.Week => "WEEK",
                FlinkTimeUnit.Day => "DAY",
                FlinkTimeUnit.Hour => "HOUR",
                FlinkTimeUnit.Minute => "MINUTE",
                FlinkTimeUnit.Second => "SECOND",
                FlinkTimeUnit.Millisecond => "MILLISECOND",
                _ => throw new NotSupportedException($"Unsupported FlinkTimeUnit: {unit}.")
            };
        }

        throw new NotSupportedException("FlinkTimeUnit must be a constant.");
    }

    private static string RenderIntervalLiteral(Expression valueExpr, Expression unitExpr)
    {
        var value = valueExpr is ConstantExpression vc ? vc.Value : null;
        if (value is null)
            throw new NotSupportedException("Interval value must be a constant.");

        return $"INTERVAL '{value}' {RenderTimeUnit(unitExpr)}";
    }

    private static string RenderIntervalExpression(Expression expr, Dictionary<ParameterExpression, string> map)
    {
        if (expr is ConstantExpression ce && ce.Value is FlinkSqlInterval interval)
            return $"INTERVAL '{interval.Value}' {RenderTimeUnit(Expression.Constant(interval.Unit))}";

        if (expr is MethodCallExpression call && call.Method.DeclaringType == typeof(FlinkSql) && call.Method.Name == nameof(FlinkSql.Interval))
            return RenderIntervalLiteral(call.Arguments[0], call.Arguments[1]);

        throw new NotSupportedException("Interval must be a FlinkSql.Interval(...) constant.");
    }

    private string RenderWindowTimeColumn(string timeColumnName)
    {
        if (string.Equals(timeColumnName, FlinkWindow.ProctimeColumnName, StringComparison.Ordinal))
            return "PROCTIME()";

        return QuoteIdentifier(NormalizeIdentifier(timeColumnName));
    }

    private string RenderSubstring(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        var instance = RenderScalar(call.Object!, map);
        var start = RenderScalar(call.Arguments[0], map);
        var startPlus1 =
            call.Arguments[0] is ConstantExpression ce && ce.Value is int i
                ? (i + 1).ToString(CultureInfo.InvariantCulture)
                : $"({start} + 1)";

        if (call.Arguments.Count == 1)
            return $"SUBSTRING({instance}, {startPlus1})";

        var len = RenderScalar(call.Arguments[1], map);
        return $"SUBSTRING({instance}, {startPlus1}, {len})";
    }

    private enum LikeMode { Contains, StartsWith, EndsWith }

    private string RenderLike(string instanceSql, Expression patternExpr, Dictionary<ParameterExpression, string> map, LikeMode mode)
    {
        if (patternExpr is ConstantExpression c && c.Value is string lit)
        {
            var escaped = EscapeLikeLiteral(lit);
            var likePattern = mode switch
            {
                LikeMode.Contains => $"%{escaped}%",
                LikeMode.StartsWith => $"{escaped}%",
                LikeMode.EndsWith => $"%{escaped}",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            var safe = likePattern.Replace("'", "''", StringComparison.Ordinal);
            return $"({instanceSql} LIKE '{safe}' ESCAPE '^')";
        }

        var arg = RenderScalar(patternExpr, map);
        var concat = mode switch
        {
            LikeMode.Contains => $"CONCAT('%', {arg}, '%')",
            LikeMode.StartsWith => $"CONCAT({arg}, '%')",
            LikeMode.EndsWith => $"CONCAT('%', {arg})",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        return $"({instanceSql} LIKE {concat})";
    }

    private static string EscapeLikeLiteral(string value)
    {
        return value
            .Replace("^", "^^", StringComparison.Ordinal)
            .Replace("%", "^%", StringComparison.Ordinal)
            .Replace("_", "^_", StringComparison.Ordinal);
    }

    private string RenderMathCall(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        return call.Method.Name switch
        {
            nameof(Math.Round) when call.Arguments.Count == 2 =>
                $"ROUND({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(Math.Floor) =>
                $"FLOOR({RenderScalar(call.Arguments[0], map)})",
            nameof(Math.Ceiling) =>
                $"CEIL({RenderScalar(call.Arguments[0], map)})",
            nameof(Math.Abs) =>
                $"ABS({RenderScalar(call.Arguments[0], map)})",
            nameof(Math.Pow) =>
                $"POWER({RenderScalar(call.Arguments[0], map)}, {RenderScalar(call.Arguments[1], map)})",
            nameof(Math.Sqrt) =>
                $"SQRT({RenderScalar(call.Arguments[0], map)})",
            nameof(Math.Log) =>
                $"LOG({RenderScalar(call.Arguments[0], map)})",
            nameof(Math.Exp) =>
                $"EXP({RenderScalar(call.Arguments[0], map)})",
            _ => throw new NotSupportedException($"Unsupported Math method: {call.Method.Name}.")
        };
    }
}
