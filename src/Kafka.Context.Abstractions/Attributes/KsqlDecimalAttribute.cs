using System;

namespace Kafka.Context.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class KsqlDecimalAttribute : Attribute
{
    public int Precision { get; }
    public int Scale { get; }

    public KsqlDecimalAttribute(int precision, int scale)
    {
        if (precision <= 0) throw new ArgumentOutOfRangeException(nameof(precision));
        if (scale < 0) throw new ArgumentOutOfRangeException(nameof(scale));
        Precision = precision;
        Scale = scale;
    }
}

