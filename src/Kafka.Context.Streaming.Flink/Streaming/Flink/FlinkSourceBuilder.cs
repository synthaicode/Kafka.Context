using Kafka.Context.Abstractions;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming.Flink;

public sealed class FlinkSourceBuilder<T> where T : class
{
    internal FlinkSourceBuilder() { }

    internal StreamingEventTimeConfig? EventTime { get; private set; }

    public FlinkSourceBuilder<T> EventTimeColumn(Expression<Func<T, object>> column, TimeSpan watermarkDelay)
    {
        var name = ExtractMemberName(column);
        EventTime = new StreamingEventTimeConfig(name, StreamingTimestampSource.PayloadColumn, watermarkDelay);
        return this;
    }

    public FlinkSourceBuilder<T> EventTimeFromKafkaTimestamp(Expression<Func<T, object>> column, TimeSpan watermarkDelay)
    {
        var name = ExtractMemberName(column);
        EventTime = new StreamingEventTimeConfig(name, StreamingTimestampSource.KafkaRecordTimestamp, watermarkDelay);
        return this;
    }

    private static string ExtractMemberName(Expression<Func<T, object>> expr)
    {
        if (expr.Body is MemberExpression m) return m.Member.Name;
        if (expr.Body is UnaryExpression u && u.NodeType == ExpressionType.Convert && u.Operand is MemberExpression m2)
            return m2.Member.Name;

        throw new ArgumentException("Expression must be a simple member access (e.g., x => x.EventTime).", nameof(expr));
    }
}

