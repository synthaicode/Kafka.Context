using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kafka.Context.Streaming.Flink;

internal sealed class FlinkFunctionRegistry
{
    public bool TryRender(
        MethodCallExpression call,
        Func<Expression, string> render,
        Dictionary<ParameterExpression, string> paramAliases,
        out string sql)
    {
        if (call.Method.DeclaringType == typeof(string))
        {
            sql = RenderStringCall(call, render);
            return true;
        }

        if (call.Method.DeclaringType == typeof(Math))
        {
            sql = RenderMathCall(call, render);
            return true;
        }

        if (call.Method.DeclaringType == typeof(FlinkSql))
        {
            sql = RenderFlinkSqlCall(call, render);
            return true;
        }

        if (call.Method.DeclaringType == typeof(FlinkWindow))
        {
            sql = RenderWindowCall(call);
            return true;
        }

        if (call.Method.DeclaringType == typeof(FlinkAgg))
        {
            sql = RenderAggCall(call, render);
            return true;
        }

        sql = string.Empty;
        return false;
    }

    private static string RenderWindowCall(MethodCallExpression call)
    {
        return call.Method.Name switch
        {
            nameof(FlinkWindow.Start) => "window_start",
            nameof(FlinkWindow.End) => "window_end",
            nameof(FlinkWindow.Proctime) => "PROCTIME()",
            _ => throw new NotSupportedException($"Unsupported FlinkWindow method: {call.Method.Name}.")
        };
    }

    private static string RenderAggCall(MethodCallExpression call, Func<Expression, string> render)
    {
        return call.Method.Name switch
        {
            nameof(FlinkAgg.Count) when call.Arguments.Count == 0 => "COUNT(*)",
            nameof(FlinkAgg.Sum) when call.Arguments.Count == 1 => $"SUM({render(call.Arguments[0])})",
            nameof(FlinkAgg.Avg) when call.Arguments.Count == 1 => $"AVG({render(call.Arguments[0])})",
            nameof(FlinkAgg.EarliestByOffset) => throw new NotSupportedException("EARLIEST_BY_OFFSET is not supported in Flink SQL."),
            nameof(FlinkAgg.LatestByOffset) => throw new NotSupportedException("LATEST_BY_OFFSET is not supported in Flink SQL."),
            _ => throw new NotSupportedException($"Unsupported FlinkAgg method: {call.Method.Name}.")
        };
    }

    private static string RenderStringCall(MethodCallExpression call, Func<Expression, string> render)
    {
        var instance = call.Object is null ? null : render(call.Object);

        return call.Method.Name switch
        {
            nameof(string.Substring) => RenderSubstring(call, render),
            nameof(string.Replace) => $"REPLACE({instance}, {render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(string.Trim) => $"TRIM({instance})",
            nameof(string.ToUpper) => $"UPPER({instance})",
            nameof(string.ToLower) => $"LOWER({instance})",
            nameof(string.IndexOf) => $"(POSITION({render(call.Arguments[0])} IN {instance}) - 1)",
            nameof(string.Contains) => RenderLike(instance!, call.Arguments[0], render, LikeMode.Contains),
            nameof(string.StartsWith) => RenderLike(instance!, call.Arguments[0], render, LikeMode.StartsWith),
            nameof(string.EndsWith) => RenderLike(instance!, call.Arguments[0], render, LikeMode.EndsWith),
            nameof(string.Split) => throw new NotSupportedException("string.Split is not supported in Flink SQL (use SPLIT_INDEX for index-based extraction)."),
            _ => throw new NotSupportedException($"Unsupported string method: {call.Method.Name}.")
        };
    }

    private static string RenderFlinkSqlCall(MethodCallExpression call, Func<Expression, string> render)
    {
        var name = call.Method.Name;
        return name switch
        {
            nameof(FlinkSql.Concat) => $"CONCAT({RenderParams(call, render)})",
            nameof(FlinkSql.Locate) => $"LOCATE({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.Position) => $"POSITION({render(call.Arguments[0])} IN {render(call.Arguments[1])})",
            nameof(FlinkSql.Lpad) => $"LPAD({render(call.Arguments[0])}, {render(call.Arguments[1])}, {render(call.Arguments[2])})",
            nameof(FlinkSql.Rpad) => $"RPAD({render(call.Arguments[0])}, {render(call.Arguments[1])}, {render(call.Arguments[2])})",
            nameof(FlinkSql.SplitIndex) => $"SPLIT_INDEX({render(call.Arguments[0])}, {render(call.Arguments[1])}, {render(call.Arguments[2])})",
            nameof(FlinkSql.RegexpExtract) => $"REGEXP_EXTRACT({render(call.Arguments[0])}, {render(call.Arguments[1])}, {render(call.Arguments[2])})",
            nameof(FlinkSql.RegexpReplace) => $"REGEXP_REPLACE({render(call.Arguments[0])}, {render(call.Arguments[1])}, {render(call.Arguments[2])})",
            nameof(FlinkSql.SimilarTo) => $"{render(call.Arguments[0])} SIMILAR TO {render(call.Arguments[1])}",
            nameof(FlinkSql.Regexp) => $"{render(call.Arguments[0])} REGEXP {render(call.Arguments[1])}",
            nameof(FlinkSql.JsonValue) => $"JSON_VALUE({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.JsonQuery) => $"JSON_QUERY({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.JsonExists) => $"JSON_EXISTS({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.JsonLength) => throw new NotSupportedException("JSON_LENGTH is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonArrayLength) => throw new NotSupportedException("JSON_ARRAY_LENGTH is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonKeys) => throw new NotSupportedException("JSON_KEYS is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonObjectKeys) => throw new NotSupportedException("JSON_OBJECT_KEYS is not supported in this Flink dialect."),
            nameof(FlinkSql.JsonTable) => throw new NotSupportedException("JSON_TABLE is not supported in this Flink dialect."),
            nameof(FlinkSql.Cast) => $"CAST({render(call.Arguments[0])} AS {RenderType(call.Arguments[1])})",
            nameof(FlinkSql.TryCast) => $"TRY_CAST({render(call.Arguments[0])} AS {RenderType(call.Arguments[1])})",
            nameof(FlinkSql.ToTimestamp) when call.Arguments.Count == 1 => $"TO_TIMESTAMP({render(call.Arguments[0])})",
            nameof(FlinkSql.ToTimestamp) when call.Arguments.Count == 2 => $"TO_TIMESTAMP({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.ToTimestampLtz) => $"TO_TIMESTAMP_LTZ({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.FromUnixtime) => $"FROM_UNIXTIME({render(call.Arguments[0])})",
            nameof(FlinkSql.CurrentTimestamp) => "CURRENT_TIMESTAMP",
            nameof(FlinkSql.DateFormat) => $"DATE_FORMAT({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.Extract) => $"EXTRACT({RenderTimeUnit(call.Arguments[0])} FROM {render(call.Arguments[1])})",
            nameof(FlinkSql.TimestampAdd) => $"TIMESTAMPADD({RenderTimeUnit(call.Arguments[0])}, {render(call.Arguments[1])}, {render(call.Arguments[2])})",
            nameof(FlinkSql.TimestampDiff) => $"TIMESTAMPDIFF({RenderTimeUnit(call.Arguments[0])}, {render(call.Arguments[1])}, {render(call.Arguments[2])})",
            nameof(FlinkSql.DayOfWeek) => $"DAYOFWEEK({render(call.Arguments[0])})",
            nameof(FlinkSql.DayOfYear) => $"DAYOFYEAR({render(call.Arguments[0])})",
            nameof(FlinkSql.WeekOfYearExtract) => $"EXTRACT(WEEK FROM {render(call.Arguments[0])})",
            nameof(FlinkSql.WeekOfYearFormat) => $"DATE_FORMAT({render(call.Arguments[0])}, 'w')",
            nameof(FlinkSql.FloorTo) => $"FLOOR({render(call.Arguments[0])} TO {RenderTimeUnit(call.Arguments[1])})",
            nameof(FlinkSql.Interval) => RenderIntervalLiteral(call.Arguments[0], call.Arguments[1]),
            nameof(FlinkSql.DateLiteral) => $"DATE {render(call.Arguments[0])}",
            nameof(FlinkSql.AddInterval) => $"{render(call.Arguments[0])} + {RenderIntervalExpression(call.Arguments[1], render)}",
            nameof(FlinkSql.SubInterval) => $"{render(call.Arguments[0])} - {RenderIntervalExpression(call.Arguments[1], render)}",
            nameof(FlinkSql.ToUtcTimestamp) => $"TO_UTC_TIMESTAMP({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.FromUtcTimestamp) => $"FROM_UTC_TIMESTAMP({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.Uuid) => "UUID()",
            nameof(FlinkSql.IfNull) => $"IFNULL({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.NullIf) => $"NULLIF({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.Like) => RenderLikePredicate(call, render),
            nameof(FlinkSql.Array) => $"ARRAY[{RenderParams(call, render)}]",
            nameof(FlinkSql.ArrayLength) => $"CARDINALITY({render(call.Arguments[0])})",
            nameof(FlinkSql.ArrayElement) => RenderArrayElement(call, render),
            nameof(FlinkSql.ArrayContains) => $"ARRAY_CONTAINS({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.ArrayDistinct) => $"ARRAY_DISTINCT({render(call.Arguments[0])})",
            nameof(FlinkSql.ArrayJoin) => $"ARRAY_JOIN({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(FlinkSql.Map) => $"MAP[{RenderParams(call, render)}]",
            nameof(FlinkSql.MapKeys) => $"MAP_KEYS({render(call.Arguments[0])})",
            nameof(FlinkSql.MapValues) => $"MAP_VALUES({render(call.Arguments[0])})",
            nameof(FlinkSql.MapGet) => $"{render(call.Arguments[0])}[{render(call.Arguments[1])}]",
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

    private static string RenderLikePredicate(MethodCallExpression call, Func<Expression, string> render)
    {
        var value = render(call.Arguments[0]);
        var pattern = render(call.Arguments[1]);
        if (call.Arguments.Count < 3)
            return $"{value} LIKE {pattern}";

        if (call.Arguments[2] is ConstantExpression ce && ce.Value is null)
            return $"{value} LIKE {pattern}";

        return $"{value} LIKE {pattern} ESCAPE {render(call.Arguments[2])}";
    }

    private static string RenderArrayElement(MethodCallExpression call, Func<Expression, string> render)
    {
        if (call.Arguments[1] is ConstantExpression ce && ce.Value is int i && i <= 0)
            throw new NotSupportedException("Array index must be 1-based in Flink SQL.");

        return $"{render(call.Arguments[0])}[{render(call.Arguments[1])}]";
    }

    private static string RenderParams(MethodCallExpression call, Func<Expression, string> render)
    {
        if (call.Arguments.Count == 1 && call.Arguments[0] is NewArrayExpression arr)
            return string.Join(", ", arr.Expressions.Select(render));

        return string.Join(", ", call.Arguments.Select(render));
    }

    private static string RenderType(Expression expr)
    {
        if (expr is ConstantExpression ce && ce.Value is FlinkSqlType type)
            return type.ToSql();

        if (expr is MemberExpression me && me.Member is PropertyInfo pi && pi.DeclaringType == typeof(FlinkSqlType))
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

    private static string RenderIntervalExpression(Expression expr, Func<Expression, string> render)
    {
        if (expr is ConstantExpression ce && ce.Value is FlinkSqlInterval interval)
            return $"INTERVAL '{interval.Value}' {RenderTimeUnit(Expression.Constant(interval.Unit))}";

        if (expr is MethodCallExpression call && call.Method.DeclaringType == typeof(FlinkSql) && call.Method.Name == nameof(FlinkSql.Interval))
            return RenderIntervalLiteral(call.Arguments[0], call.Arguments[1]);

        throw new NotSupportedException("Interval must be a FlinkSql.Interval(...) constant.");
    }

    private static string RenderSubstring(MethodCallExpression call, Func<Expression, string> render)
    {
        var instance = render(call.Object!);
        var start = render(call.Arguments[0]);
        var startPlus1 =
            call.Arguments[0] is ConstantExpression ce && ce.Value is int i
                ? (i + 1).ToString(CultureInfo.InvariantCulture)
                : $"({start} + 1)";

        if (call.Arguments.Count == 1)
            return $"SUBSTRING({instance}, {startPlus1})";

        var len = render(call.Arguments[1]);
        return $"SUBSTRING({instance}, {startPlus1}, {len})";
    }

    private enum LikeMode { Contains, StartsWith, EndsWith }

    private static string RenderLike(string instanceSql, Expression patternExpr, Func<Expression, string> render, LikeMode mode)
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

        var arg = render(patternExpr);
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

    private static string RenderMathCall(MethodCallExpression call, Func<Expression, string> render)
    {
        return call.Method.Name switch
        {
            nameof(Math.Round) when call.Arguments.Count == 2 =>
                $"ROUND({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(Math.Floor) =>
                $"FLOOR({render(call.Arguments[0])})",
            nameof(Math.Ceiling) =>
                $"CEIL({render(call.Arguments[0])})",
            nameof(Math.Abs) =>
                $"ABS({render(call.Arguments[0])})",
            nameof(Math.Pow) =>
                $"POWER({render(call.Arguments[0])}, {render(call.Arguments[1])})",
            nameof(Math.Sqrt) =>
                $"SQRT({render(call.Arguments[0])})",
            nameof(Math.Log) =>
                $"LOG({render(call.Arguments[0])})",
            nameof(Math.Exp) =>
                $"EXP({render(call.Arguments[0])})",
            _ => throw new NotSupportedException($"Unsupported Math method: {call.Method.Name}.")
        };
    }
}
