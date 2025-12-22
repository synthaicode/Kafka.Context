using System;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming.Flink;

public static class FlinkWindowExtensions
{
    public static IStreamingQueryable<T> TumbleWindow<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, object>> timeColumn,
        TimeSpan size)
        where T : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        var colName = ExtractMemberName(timeColumn);
        q.PlanBuilder.SetWindow(new StreamingWindowSpec(StreamingWindowKind.Tumble, colName, size, Slide: null));
        return source;
    }

    public static IStreamingQueryable<T> HopWindow<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, object>> timeColumn,
        TimeSpan slide,
        TimeSpan size)
        where T : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        var colName = ExtractMemberName(timeColumn);
        q.PlanBuilder.SetWindow(new StreamingWindowSpec(StreamingWindowKind.Hop, colName, size, slide));
        return source;
    }

    public static IStreamingQueryable<T> SessionWindow<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, object>> timeColumn,
        TimeSpan gap)
        where T : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        var colName = ExtractMemberName(timeColumn);
        q.PlanBuilder.SetWindow(new StreamingWindowSpec(StreamingWindowKind.Session, colName, gap, Slide: null));
        return source;
    }

    private static string ExtractMemberName<T>(Expression<Func<T, object>> expr)
    {
        if (expr is null) throw new ArgumentNullException(nameof(expr));

        Expression body = expr.Body;
        while (body is UnaryExpression u && (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
            body = u.Operand;

        if (body is MemberExpression m)
            return m.Member.Name;

        if (body is MethodCallExpression call && call.Method.DeclaringType == typeof(FlinkWindow) && call.Method.Name == nameof(FlinkWindow.Proctime))
            return FlinkWindow.ProctimeColumnName;

        throw new NotSupportedException("timeColumn must be a member access expression (e.g., x => x.Timestamp) or FlinkWindow.Proctime().");
    }
}
