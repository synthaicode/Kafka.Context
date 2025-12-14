using System;

namespace Kafka.Context.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class KafkaDecimalAttribute : Attribute
{
    public int Precision { get; }
    public int Scale { get; }

    public KafkaDecimalAttribute(int precision, int scale)
    {
        if (precision <= 0) throw new ArgumentOutOfRangeException(nameof(precision));
        if (scale < 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (scale > precision) throw new ArgumentOutOfRangeException(nameof(scale), "scale must be <= precision");

        Precision = precision;
        Scale = scale;
    }
}

