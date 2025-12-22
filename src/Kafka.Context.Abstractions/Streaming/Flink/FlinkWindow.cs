namespace Kafka.Context.Streaming.Flink;

public static class FlinkWindow
{
    public const string ProctimeColumnName = "PROCTIME()";

    public static object Start() => throw new NotSupportedException("Use in ToQuery only.");
    public static object End() => throw new NotSupportedException("Use in ToQuery only.");
    public static object Proctime() => throw new NotSupportedException("Use in ToQuery only.");
}
