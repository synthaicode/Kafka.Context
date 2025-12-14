using Kafka.Context.Attributes;
using System.Reflection;
using System.Text.Json;

namespace Kafka.Context.Infrastructure.SchemaRegistry;

internal static class AvroSchemaBuilder
{
    public static string BuildValueSchema(Type entityType)
        => BuildRecordSchema(entityType, properties: GetPublicReadableProperties(entityType));

    public static string BuildKeySchema(Type entityType)
    {
        var keyProps = GetKeyProperties(entityType);

        if (keyProps.Count == 0)
            return JsonSerializer.Serialize("string");

        return BuildRecordSchema(entityType, properties: keyProps, suffix: "Key");
    }

    public static int GetKeyPropertyCount(Type entityType)
        => GetKeyProperties(entityType).Count;

    public static string GetRecordFullName(string schemaJson)
    {
        using var doc = JsonDocument.Parse(schemaJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return string.Empty;

        if (!doc.RootElement.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            return string.Empty;

        if (!string.Equals(typeEl.GetString(), "record", StringComparison.Ordinal))
            return string.Empty;

        var name = doc.RootElement.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
            ? nameEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var ns = doc.RootElement.TryGetProperty("namespace", out var nsEl) && nsEl.ValueKind == JsonValueKind.String
            ? nsEl.GetString()
            : null;

        return string.IsNullOrWhiteSpace(ns) ? name! : $"{ns}.{name}";
    }

    private static string BuildRecordSchema(Type entityType, IReadOnlyList<PropertyInfo> properties, string? suffix = null)
    {
        var recordName = suffix is null ? entityType.Name : $"{entityType.Name}{suffix}";
        var recordNamespace = string.IsNullOrWhiteSpace(entityType.Namespace) ? "Kafka.Context" : entityType.Namespace!;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("type", "record");
        writer.WriteString("name", recordName);
        writer.WriteString("namespace", recordNamespace);

        writer.WritePropertyName("fields");
        writer.WriteStartArray();

        foreach (var property in properties)
        {
            writer.WriteStartObject();
            writer.WriteString("name", property.Name);

            writer.WritePropertyName("type");
            WriteAvroType(writer, property);

            if (IsNullable(property.PropertyType))
                writer.WriteNull("default");

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteAvroType(Utf8JsonWriter writer, PropertyInfo property)
    {
        var type = property.PropertyType;
        var isNullable = IsNullable(type);
        var coreType = Nullable.GetUnderlyingType(type) ?? type;

        if (isNullable)
        {
            writer.WriteStartArray();
            writer.WriteStringValue("null");
            WriteNonNullableType(writer, coreType, property);
            writer.WriteEndArray();
            return;
        }

        WriteNonNullableType(writer, coreType, property);
    }

    private static void WriteNonNullableType(Utf8JsonWriter writer, Type coreType, PropertyInfo property)
    {
        if (coreType == typeof(int))
        {
            writer.WriteStringValue("int");
            return;
        }

        if (coreType == typeof(long))
        {
            writer.WriteStringValue("long");
            return;
        }

        if (coreType == typeof(string))
        {
            writer.WriteStringValue("string");
            return;
        }

        if (coreType == typeof(bool))
        {
            writer.WriteStringValue("boolean");
            return;
        }

        if (TryGetDictionaryTypes(coreType, out var keyType, out var valueType))
        {
            if (keyType != typeof(string) || valueType != typeof(string))
            {
                throw new NotSupportedException(
                    $"Unsupported dictionary type '{coreType.FullName}' for Avro schema generation: {property.DeclaringType?.FullName}.{property.Name}. Only Dictionary<string, string> is supported.");
            }

            writer.WriteStartObject();
            writer.WriteString("type", "map");
            writer.WriteString("values", "string");
            writer.WriteEndObject();
            return;
        }

        if (coreType == typeof(DateTime) || property.GetCustomAttribute<KsqlTimestampAttribute>(inherit: true) is not null)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "long");
            writer.WriteString("logicalType", "timestamp-millis");
            writer.WriteEndObject();
            return;
        }

        if (coreType == typeof(decimal))
        {
            var dec = property.GetCustomAttribute<KsqlDecimalAttribute>(inherit: true)
                      ?? throw new InvalidOperationException($"decimal field '{property.DeclaringType?.FullName}.{property.Name}' requires [{nameof(KsqlDecimalAttribute)}].");

            writer.WriteStartObject();
            writer.WriteString("type", "bytes");
            writer.WriteString("logicalType", "decimal");
            writer.WriteNumber("precision", dec.Precision);
            writer.WriteNumber("scale", dec.Scale);
            writer.WriteEndObject();
            return;
        }

        throw new NotSupportedException($"Unsupported property type '{coreType.FullName}' for Avro schema generation: {property.DeclaringType?.FullName}.{property.Name}");
    }

    private static bool TryGetDictionaryTypes(Type type, out Type? keyType, out Type? valueType)
    {
        keyType = null;
        valueType = null;

        Type? dictInterface = null;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            dictInterface = type;
        else
            dictInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (dictInterface is null)
            return false;

        var args = dictInterface.GetGenericArguments();
        if (args.Length != 2)
            return false;

        keyType = args[0];
        valueType = args[1];
        return true;
    }

    private static bool IsNullable(Type type)
    {
        if (!type.IsValueType) return true;
        return Nullable.GetUnderlyingType(type) is not null;
    }

    private static IReadOnlyList<PropertyInfo> GetPublicReadableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetMethod is not null && p.GetIndexParameters().Length == 0)
            .ToList();
    }

    private static IReadOnlyList<PropertyInfo> GetKeyProperties(Type entityType)
    {
        return GetPublicReadableProperties(entityType)
            .Where(p => p.GetCustomAttribute<KsqlKeyAttribute>(inherit: true) is not null)
            .ToList();
    }
}
