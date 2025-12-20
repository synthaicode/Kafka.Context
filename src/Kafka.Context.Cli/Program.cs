using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.SchemaRegistry;
using Kafka.Context.Attributes;

namespace Kafka.Context;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args is ["-h"] or ["--help"])
            {
                PrintHelp();
                return 0;
            }

            if (args.Length >= 2 && string.Equals(args[0], "schema", StringComparison.OrdinalIgnoreCase))
            {
                var sub = args[1];
                var subArgs = args.Skip(2).ToArray();

                if (string.Equals(sub, "scaffold", StringComparison.OrdinalIgnoreCase))
                    return await RunScaffoldAsync(subArgs).ConfigureAwait(false);

                if (string.Equals(sub, "verify", StringComparison.OrdinalIgnoreCase))
                    return await RunVerifyAsync(subArgs).ConfigureAwait(false);

                if (string.Equals(sub, "subjects", StringComparison.OrdinalIgnoreCase))
                    return await RunSubjectsAsync(subArgs).ConfigureAwait(false);
            }

            Console.Error.WriteLine("Unknown command.");
            PrintHelp();
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("kafka-context (dotnet tool)");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  kafka-context schema scaffold --subject <subject> [options]");
        Console.WriteLine("  kafka-context schema verify   --subject <subject> [--type <assembly-qualified-type> | --fingerprint <hex>] [options]");
        Console.WriteLine("  kafka-context schema subjects [options]");
        Console.WriteLine();
        Console.WriteLine("Options (shared):");
        Console.WriteLine("  --sr-url <url>       Schema Registry URL (or env KAFKA_CONTEXT_SCHEMA_REGISTRY_URL)");
        Console.WriteLine();
        Console.WriteLine("Options (scaffold):");
        Console.WriteLine("  --output <dir>       Output directory (default: ./)");
        Console.WriteLine("  --namespace <ns>     Generated namespace (default: Kafka.Context.Generated)");
        Console.WriteLine("  --style <record|class>  Generated type style (default: record)");
        Console.WriteLine("  --topic <topic>      Override topic name (default: derived from subject)");
        Console.WriteLine("  --force              Overwrite existing files");
        Console.WriteLine("  --dry-run            Print generated code to stdout (no file write)");
        Console.WriteLine();
        Console.WriteLine("Options (verify):");
        Console.WriteLine("  --type <type>        Assembly-qualified type name (optional)");
        Console.WriteLine("  --fingerprint <hex>  Expected fingerprint (optional)");
        Console.WriteLine();
        Console.WriteLine("Options (subjects):");
        Console.WriteLine("  --prefix <prefix>    Filter subjects by prefix");
        Console.WriteLine("  --contains <text>    Filter subjects by substring");
        Console.WriteLine("  --json               Output JSON array");
    }

    private static async Task<int> RunScaffoldAsync(string[] args)
    {
        var subject = GetOption(args, "--subject");
        if (string.IsNullOrWhiteSpace(subject))
        {
            Console.Error.WriteLine("--subject is required.");
            return 2;
        }

        var srUrl = GetOption(args, "--sr-url") ?? Environment.GetEnvironmentVariable("KAFKA_CONTEXT_SCHEMA_REGISTRY_URL");
        if (string.IsNullOrWhiteSpace(srUrl))
        {
            Console.Error.WriteLine("--sr-url is required (or env KAFKA_CONTEXT_SCHEMA_REGISTRY_URL).");
            return 2;
        }

        var output = GetOption(args, "--output") ?? ".";
        var @namespace = GetOption(args, "--namespace") ?? "Kafka.Context.Generated";
        var style = GetOption(args, "--style") ?? "record";
        var topic = GetOption(args, "--topic") ?? TryDeriveTopicFromSubject(subject);
        var force = HasFlag(args, "--force");
        var dryRun = HasFlag(args, "--dry-run");

        if (!string.Equals(style, "record", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(style, "class", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--style must be 'record' or 'class'.");
            return 2;
        }

        using var client = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = srUrl });
        var latest = await client.GetLatestSchemaAsync(subject).ConfigureAwait(false);
        var schemaJson = latest.SchemaString;

        var fingerprint = SchemaFingerprint.Compute(schemaJson);
        var generated = AvroToCSharpGenerator.Generate(
            schemaJson: schemaJson,
            @namespace: @namespace,
            topicName: topic,
            subject: subject,
            fingerprint: fingerprint,
            style: style);

        if (dryRun)
        {
            Console.WriteLine(generated.Code);
            return 0;
        }

        Directory.CreateDirectory(output);

        var path = Path.Combine(output, generated.FileName);
        if (File.Exists(path) && !force)
        {
            Console.Error.WriteLine($"File already exists (use --force): {path}");
            return 3;
        }

        await File.WriteAllTextAsync(path, generated.Code, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)).ConfigureAwait(false);
        Console.WriteLine($"Generated: {path}");
        return 0;
    }

    private static async Task<int> RunVerifyAsync(string[] args)
    {
        var subject = GetOption(args, "--subject");
        if (string.IsNullOrWhiteSpace(subject))
        {
            Console.Error.WriteLine("--subject is required.");
            return 2;
        }

        var srUrl = GetOption(args, "--sr-url") ?? Environment.GetEnvironmentVariable("KAFKA_CONTEXT_SCHEMA_REGISTRY_URL");
        if (string.IsNullOrWhiteSpace(srUrl))
        {
            Console.Error.WriteLine("--sr-url is required (or env KAFKA_CONTEXT_SCHEMA_REGISTRY_URL).");
            return 2;
        }

        var expectedFingerprint = GetOption(args, "--fingerprint");
        var typeName = GetOption(args, "--type");

        if (string.IsNullOrWhiteSpace(expectedFingerprint))
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                Console.Error.WriteLine("Either --fingerprint or --type is required.");
                return 2;
            }

            var type = Type.GetType(typeName, throwOnError: false);
            if (type is null)
            {
                Console.Error.WriteLine($"Type not found: {typeName}");
                Console.Error.WriteLine("Tip: provide --fingerprint if the target assembly is not loadable from the current process.");
                return 2;
            }

            var expectedSubject = type.GetCustomAttribute<SchemaSubjectAttribute>()?.Subject;
            expectedFingerprint = type.GetCustomAttribute<SchemaFingerprintAttribute>()?.Fingerprint;

            if (string.IsNullOrWhiteSpace(expectedFingerprint))
            {
                Console.Error.WriteLine($"Missing [{nameof(SchemaFingerprintAttribute)}] on type: {type.FullName}");
                return 2;
            }

            if (!string.IsNullOrWhiteSpace(expectedSubject) && !string.Equals(expectedSubject, subject, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Subject mismatch: --subject='{subject}' but attribute='{expectedSubject}'");
                return 2;
            }
        }

        using var client = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = srUrl });
        var latest = await client.GetLatestSchemaAsync(subject).ConfigureAwait(false);

        var actualFingerprint = SchemaFingerprint.Compute(latest.SchemaString);

        if (string.Equals(expectedFingerprint, actualFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("OK: Schema fingerprint matches.");
            return 0;
        }

        Console.Error.WriteLine($"Schema contract mismatch for '{subject}':");
        Console.Error.WriteLine($"  Expected fingerprint: {expectedFingerprint}");
        Console.Error.WriteLine($"  Actual fingerprint:   {actualFingerprint}");
        return 4;
    }

    private static async Task<int> RunSubjectsAsync(string[] args)
    {
        var srUrl = GetOption(args, "--sr-url") ?? Environment.GetEnvironmentVariable("KAFKA_CONTEXT_SCHEMA_REGISTRY_URL");
        if (string.IsNullOrWhiteSpace(srUrl))
        {
            Console.Error.WriteLine("--sr-url is required (or env KAFKA_CONTEXT_SCHEMA_REGISTRY_URL).");
            return 2;
        }

        var prefix = GetOption(args, "--prefix");
        var contains = GetOption(args, "--contains");
        var asJson = HasFlag(args, "--json");

        using var client = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = srUrl });
        var subjects = (await client.GetAllSubjectsAsync().ConfigureAwait(false))
            .OrderBy(s => s, StringComparer.Ordinal)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(prefix))
            subjects = subjects.Where(s => s.StartsWith(prefix, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(contains))
            subjects = subjects.Where(s => s.Contains(contains, StringComparison.Ordinal));

        var result = subjects.ToArray();

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(result));
            return 0;
        }

        foreach (var subject in result)
            Console.WriteLine(subject);

        return 0;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Length) return string.Empty;
            return args[i + 1];
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name)
        => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? TryDeriveTopicFromSubject(string subject)
    {
        if (subject.EndsWith("-value", StringComparison.Ordinal))
            return subject[..^"-value".Length];
        if (subject.EndsWith("-key", StringComparison.Ordinal))
            return subject[..^"-key".Length];
        return null;
    }

    private static class SchemaFingerprint
    {
        public static string Compute(string schemaJson)
        {
            using var doc = JsonDocument.Parse(schemaJson);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteNormalized(writer, doc.RootElement);
            }

            var normalized = stream.ToArray();
            var hash = SHA256.HashData(normalized);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static void WriteNormalized(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(prop.Name);
                        WriteNormalized(writer, prop.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        WriteNormalized(writer, item);
                    }
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l))
                        writer.WriteNumberValue(l);
                    else
                        writer.WriteNumberValue(element.GetDouble());
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    writer.WriteNullValue();
                    break;
                default:
                    writer.WriteNullValue();
                    break;
            }
        }
    }

    private static class AvroToCSharpGenerator
    {
        public static GeneratedFile Generate(
            string schemaJson,
            string @namespace,
            string? topicName,
            string subject,
            string fingerprint,
            string style)
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Unsupported schema JSON root (expected object).");

            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (!string.Equals(type, "record", StringComparison.Ordinal))
                throw new InvalidOperationException("Only Avro 'record' root schema is supported.");

            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Schema root 'name' is required.");

            var kind = string.Equals(style, "class", StringComparison.OrdinalIgnoreCase) ? "class" : "record";

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Kafka.Context.Attributes;");
            sb.AppendLine();
            sb.AppendLine($"namespace {@namespace};");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(topicName))
                sb.AppendLine($"[KafkaTopic(\"{EscapeString(topicName)}\")]");
            sb.AppendLine($"[SchemaSubject(\"{EscapeString(subject)}\")]");
            sb.AppendLine($"[SchemaFingerprint(\"{EscapeString(fingerprint)}\")]");
            sb.AppendLine($"public sealed {kind} {SanitizeIdentifier(name)}");
            sb.AppendLine("{");

            var nested = new List<string>();

            if (!root.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Schema root 'fields' must be an array.");

            foreach (var field in fields.EnumerateArray())
            {
                if (field.ValueKind != JsonValueKind.Object) continue;
                var fieldName = field.TryGetProperty("name", out var fn) ? fn.GetString() : null;
                if (string.IsNullOrWhiteSpace(fieldName)) continue;

                if (!field.TryGetProperty("type", out var ft))
                    continue;

                var ctx = new GenerationContext(nested);
                var csType = MapType(ft, ctx, parentTypeName: name);
                var propertyName = ToPascalCase(fieldName);

                sb.AppendLine($"    public {csType} {SanitizeIdentifier(propertyName)} {{ get; init; }} = default!;");
                sb.AppendLine();
            }

            foreach (var nestedDecl in nested)
            {
                sb.AppendLine();
                sb.AppendLine(nestedDecl);
            }

            sb.AppendLine("}");

            return new GeneratedFile($"{SanitizeFileName(name)}.g.cs", sb.ToString());
        }

        private static string MapType(JsonElement typeElement, GenerationContext ctx, string parentTypeName)
        {
            // Union: ["null", "..."]
            if (typeElement.ValueKind == JsonValueKind.Array)
            {
                var items = typeElement.EnumerateArray().ToArray();
                var hasNull = items.Any(i => i.ValueKind == JsonValueKind.String && string.Equals(i.GetString(), "null", StringComparison.Ordinal));
                var nonNull = items.Where(i => !(i.ValueKind == JsonValueKind.String && string.Equals(i.GetString(), "null", StringComparison.Ordinal))).ToArray();
                if (hasNull && nonNull.Length == 1)
                {
                    var inner = MapType(nonNull[0], ctx, parentTypeName);
                    return MakeNullable(inner);
                }

                return "object";
            }

            if (typeElement.ValueKind == JsonValueKind.String)
            {
                return MapPrimitive(typeElement.GetString() ?? string.Empty);
            }

            if (typeElement.ValueKind != JsonValueKind.Object)
                return "object";

            var objType = typeElement.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(objType))
                return "object";

            // logical types
            if (typeElement.TryGetProperty("logicalType", out var logicalType))
            {
                var lt = logicalType.GetString();
                if (string.Equals(lt, "decimal", StringComparison.Ordinal))
                    return "decimal";
                if (string.Equals(lt, "uuid", StringComparison.Ordinal))
                    return "Guid";
                if (string.Equals(lt, "date", StringComparison.Ordinal))
                    return "DateOnly";
                if (string.Equals(lt, "timestamp-millis", StringComparison.Ordinal) || string.Equals(lt, "timestamp-micros", StringComparison.Ordinal))
                    return "DateTime";
            }

            if (string.Equals(objType, "array", StringComparison.Ordinal))
            {
                var itemType = typeElement.TryGetProperty("items", out var itemsEl) ? MapType(itemsEl, ctx, parentTypeName) : "object";
                return $"IReadOnlyList<{itemType}>";
            }

            if (string.Equals(objType, "map", StringComparison.Ordinal))
            {
                var valueType = typeElement.TryGetProperty("values", out var valuesEl) ? MapType(valuesEl, ctx, parentTypeName) : "object";
                return $"IReadOnlyDictionary<string, {valueType}>";
            }

            if (string.Equals(objType, "enum", StringComparison.Ordinal))
            {
                var enumName = typeElement.TryGetProperty("name", out var en) ? en.GetString() : null;
                enumName ??= "Enum";
                var safeEnumName = SanitizeIdentifier(enumName);
                var symbols = typeElement.TryGetProperty("symbols", out var syms) && syms.ValueKind == JsonValueKind.Array
                    ? syms.EnumerateArray().Select(s => s.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>().ToArray()
                    : Array.Empty<string>();

                var sb = new StringBuilder();
                sb.AppendLine($"    public enum {safeEnumName}");
                sb.AppendLine("    {");
                for (var i = 0; i < symbols.Length; i++)
                {
                    var member = SanitizeIdentifier(ToPascalCase(symbols[i]));
                    var comma = i == symbols.Length - 1 ? string.Empty : ",";
                    sb.AppendLine($"        {member}{comma}");
                }
                sb.AppendLine("    }");

                ctx.NestedDeclarations.Add(sb.ToString());
                return safeEnumName;
            }

            if (string.Equals(objType, "record", StringComparison.Ordinal))
            {
                var recordName = typeElement.TryGetProperty("name", out var rn) ? rn.GetString() : null;
                recordName ??= $"{parentTypeName}Record";
                var safeName = SanitizeIdentifier(recordName);

                if (!typeElement.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
                    return safeName;

                var sb = new StringBuilder();
                sb.AppendLine($"    public sealed record {safeName}");
                sb.AppendLine("    {");

                foreach (var f in fields.EnumerateArray())
                {
                    if (f.ValueKind != JsonValueKind.Object) continue;
                    var fieldName = f.TryGetProperty("name", out var fn) ? fn.GetString() : null;
                    if (string.IsNullOrWhiteSpace(fieldName)) continue;
                    if (!f.TryGetProperty("type", out var ft)) continue;

                    var innerType = MapType(ft, ctx, parentTypeName: recordName);
                    sb.AppendLine($"        public {innerType} {SanitizeIdentifier(ToPascalCase(fieldName))} {{ get; init; }} = default!;");
                }

                sb.AppendLine("    }");

                ctx.NestedDeclarations.Add(sb.ToString());
                return safeName;
            }

            return MapPrimitive(objType);
        }

        private static string MapPrimitive(string avroType)
            => avroType switch
            {
                "null" => "object?",
                "boolean" => "bool",
                "int" => "int",
                "long" => "long",
                "float" => "float",
                "double" => "double",
                "bytes" => "byte[]",
                "string" => "string",
                _ => "object"
            };

        private static string MakeNullable(string csType)
        {
            if (csType.EndsWith("?", StringComparison.Ordinal)) return csType;
            if (csType is "int" or "long" or "float" or "double" or "bool" or "decimal" or "Guid" or "DateOnly" or "DateTime")
                return csType + "?";
            return csType + "?";
        }

        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var parts = name.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
        }

        private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string SanitizeFileName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "_";

            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (i == 0)
                {
                    if (char.IsLetter(ch) || ch == '_') sb.Append(ch);
                    else sb.Append('_');
                    continue;
                }

                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            }

            return sb.ToString();
        }

        private sealed record GenerationContext(List<string> NestedDeclarations);
    }

    private sealed record GeneratedFile(string FileName, string Code);
}
