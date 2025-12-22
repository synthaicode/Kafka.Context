using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kafka.Context.Streaming.Flink;

internal sealed class FlinkExpressionVisitor
{
    private readonly FlinkFunctionRegistry _functions;

    public FlinkExpressionVisitor(FlinkFunctionRegistry functions)
    {
        _functions = functions;
    }

    public string Render(Expression expression, Dictionary<ParameterExpression, string> paramAliases)
    {
        return expression.NodeType switch
        {
            ExpressionType.Constant => RenderConstant((ConstantExpression)expression),
            ExpressionType.MemberAccess => RenderMemberAccess((MemberExpression)expression, paramAliases),
            ExpressionType.Convert or ExpressionType.ConvertChecked => Render(((UnaryExpression)expression).Operand, paramAliases),
            ExpressionType.New => RenderNew((NewExpression)expression, paramAliases),
            ExpressionType.MemberInit => RenderMemberInit((MemberInitExpression)expression, paramAliases),
            ExpressionType.Not => $"(NOT {Render(((UnaryExpression)expression).Operand, paramAliases)})",
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
        => string.Join(", ", expr.Arguments.Select(a => Render(a, map)));

    private string RenderMemberInit(MemberInitExpression expr, Dictionary<ParameterExpression, string> map)
    {
        var parts = expr.Bindings
            .OfType<MemberAssignment>()
            .Select(b => Render(b.Expression, map))
            .ToArray();

        return string.Join(", ", parts);
    }

    private string RenderBinary(BinaryExpression binary, string op, Dictionary<ParameterExpression, string> map)
        => $"({Render(binary.Left, map)} {op} {Render(binary.Right, map)})";

    private string RenderCoalesce(BinaryExpression binary, Dictionary<ParameterExpression, string> map)
        => $"COALESCE({Render(binary.Left, map)}, {Render(binary.Right, map)})";

    private string RenderConditional(ConditionalExpression expression, Dictionary<ParameterExpression, string> map)
    {
        var test = Render(expression.Test, map);
        var ifTrue = Render(expression.IfTrue, map);
        var ifFalse = Render(expression.IfFalse, map);
        return $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";
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
            var col = FlinkSqlIdentifiers.QuoteIdentifier(FlinkSqlIdentifiers.NormalizeIdentifier(m.Member.Name));
            return $"{alias}.{col}";
        }

        if (m.Member.DeclaringType == typeof(string) && m.Member.Name == nameof(string.Length))
            return $"CHAR_LENGTH({Render(m.Expression!, map)})";

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
                return $"EXTRACT({part} FROM {Render(m.Expression!, map)})";
        }

        throw new NotSupportedException($"Unsupported member access: {m.Member.DeclaringType?.FullName}.{m.Member.Name}.");
    }

    private string RenderCall(MethodCallExpression call, Dictionary<ParameterExpression, string> map)
    {
        if (_functions.TryRender(call, expr => Render(expr, map), map, out var sql))
            return sql;

        throw new NotSupportedException($"Unsupported method call: {call.Method.DeclaringType?.FullName}.{call.Method.Name}.");
    }
}
