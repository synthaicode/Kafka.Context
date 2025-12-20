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
            if (value is DateTime dt)
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

            if (value is DateTimeOffset dto)
                return dto.UtcDateTime;

            var millis = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
        }

        if (targetType == typeof(decimal))
        {
            var attr = property.GetCustomAttribute<KafkaDecimalAttribute>(inherit: true)
                       ?? throw new InvalidOperationException($"decimal field '{property.DeclaringType?.FullName}.{property.Name}' requires [KafkaDecimal].");

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

            if (value is object[] array)
            {
                var converted = new Dictionary<string, string>(StringComparer.Ordinal);

                if (array.Length == 2 && array[0] is string singleKey)
                {
                    converted[singleKey] = array[1]?.ToString() ?? string.Empty;
                    return converted;
                }

                foreach (var item in array)
                {
                    if (item is null) continue;

                    if (item is KeyValuePair<string, string> kvStr)
                    {
                        converted[kvStr.Key] = kvStr.Value;
                        continue;
                    }

                    if (item is KeyValuePair<string, object?> kvObj)
                    {
                        converted[kvObj.Key] = kvObj.Value?.ToString() ?? string.Empty;
                        continue;
                    }

                    if (item is GenericRecord record)
                    {
                        var keyField = record.Schema.Fields.FirstOrDefault(f => string.Equals(f.Name, "key", StringComparison.OrdinalIgnoreCase));
                        var valueField = record.Schema.Fields.FirstOrDefault(f => string.Equals(f.Name, "value", StringComparison.OrdinalIgnoreCase));
                        if (keyField is null || valueField is null)
                            continue;

                        var k = record[keyField.Name]?.ToString() ?? string.Empty;
                        var v = record[valueField.Name]?.ToString() ?? string.Empty;
                        converted[k] = v;
                        continue;
                    }
                }

                if (converted.Count > 0)
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
