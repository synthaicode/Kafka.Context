namespace Kafka.Context.Streaming.Flink;

public readonly record struct FlinkSqlType(string Name, int? Precision = null, int? Scale = null)
{
    public string ToSql()
    {
        if (Precision is null)
            return Name;

        return Scale is null
            ? $"{Name}({Precision.Value})"
            : $"{Name}({Precision.Value}, {Scale.Value})";
    }

    public static FlinkSqlType Int => new("INT");
    public static FlinkSqlType BigInt => new("BIGINT");
    public static FlinkSqlType Double => new("DOUBLE");
    public static FlinkSqlType Boolean => new("BOOLEAN");
    public static FlinkSqlType String => new("STRING");
    public static FlinkSqlType Timestamp => new("TIMESTAMP");

    public static FlinkSqlType TimestampLtz(int precision) => new("TIMESTAMP_LTZ", precision);
    public static FlinkSqlType Decimal(int precision, int scale) => new("DECIMAL", precision, scale);
}
