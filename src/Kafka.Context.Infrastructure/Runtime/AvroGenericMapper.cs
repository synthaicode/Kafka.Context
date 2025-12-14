using Avro.Generic;
using Kafka.Context.Attributes;
using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;

namespace Kafka.Context.Infrastructure.Runtime;

internal static class AvroGenericMapper
{
    public static T MapTo<T>(GenericRecord record)
    {
        var entity = Activator.CreateInstance<T>();
        var entityType = typeof(T);

        foreach (var field in record.Schema.Fields)
        {
            var property = entityType.GetProperty(field.Name, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.SetMethod is null)
                continue;

            var value = record[field.Name];
            property.SetValue(entity, ConvertValue(property, value));
        }

        return entity;
    }

    private static object? ConvertValue(PropertyInfo property, object? value)
    {
        if (value is null) return null;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (targetType == typeof(DateTime))
        {
            var millis = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
        }

        if (targetType == typeof(decimal))
        {
            var attr = property.GetCustomAttribute<KsqlDecimalAttribute>(inherit: true)
                       ?? throw new InvalidOperationException($"decimal field '{property.DeclaringType?.FullName}.{property.Name}' requires [KsqlDecimal].");

            BigInteger unscaled;
            int scale;

            if (value is Avro.AvroDecimal avroDec)
            {
                unscaled = avroDec.UnscaledValue;
                scale = avroDec.Scale;
            }
            else
            {
                var bytes = (byte[])value;
                unscaled = new BigInteger(bytes, isUnsigned: false, isBigEndian: true);
                scale = attr.Scale;
            }

            var factor = 1m;
            for (var i = 0; i < scale; i++)
                factor *= 10m;

            var dec = (decimal)unscaled;
            return dec / factor;
        }

        if (targetType == typeof(Dictionary<string, string>))
        {
            if (value is IDictionary<string, string> mapStr)
                return new Dictionary<string, string>(mapStr, StringComparer.Ordinal);

            if (value is IDictionary<string, object?> mapObj)
            {
                var converted = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in mapObj)
                    converted[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                return converted;
            }

            if (value is IDictionary map)
            {
                var converted = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in map)
                {
                    if (entry.Key is null) continue;
                    converted[entry.Key.ToString() ?? string.Empty] = entry.Value?.ToString() ?? string.Empty;
                }
                return converted;
            }
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }
}
