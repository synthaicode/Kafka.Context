namespace Kafka.Context.Streaming.Flink;

public static class FlinkAgg
{
    public static long Count() => throw new NotSupportedException("Use in ToQuery only.");

    public static int Sum(int value) => throw new NotSupportedException("Use in ToQuery only.");
    public static long Sum(long value) => throw new NotSupportedException("Use in ToQuery only.");
    public static decimal Sum(decimal value) => throw new NotSupportedException("Use in ToQuery only.");

    public static double Avg(int value) => throw new NotSupportedException("Use in ToQuery only.");
    public static double Avg(long value) => throw new NotSupportedException("Use in ToQuery only.");
    public static double Avg(double value) => throw new NotSupportedException("Use in ToQuery only.");
    public static double Avg(decimal value) => throw new NotSupportedException("Use in ToQuery only.");

    public static T EarliestByOffset<T>(T value) => throw new NotSupportedException("Use in ToQuery only.");
    public static T LatestByOffset<T>(T value) => throw new NotSupportedException("Use in ToQuery only.");
}
