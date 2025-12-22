using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.SchemaRegistry;
using Kafka.Context.Configuration;
using Kafka.Context.Attributes;
using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;
using Microsoft.Extensions.Configuration;

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

            if (args.Length >= 3 && string.Equals(args[0], "streaming", StringComparison.OrdinalIgnoreCase))
            {
                var dialect = args[1];
                var sub = args[2];
                var subArgs = args.Skip(3).ToArray();

                if (string.Equals(dialect, "flink", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(sub, "with-preview", StringComparison.OrdinalIgnoreCase))
                {
                    return RunFlinkWithPreview(subArgs);
                }
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
        Console.WriteLine("  kafka-context streaming flink with-preview [options]");
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
        Console.WriteLine();
        Console.WriteLine("Options (streaming flink with-preview):");
        Console.WriteLine("  --config <path>      appsettings.json path (default: ./appsettings.json)");
        Console.WriteLine("  --topic <name>       Only show a single topic (optional)");
        Console.WriteLine("  --kind <all|source|sink>  Filter by kind (default: all)");
        Console.WriteLine("  --assembly <path[,path]>  Assembly path(s) to load POCOs for column DDL");
        Console.WriteLine("  --allow-missing-types  Allow topics without matching POCOs (fallback to skeleton)");
        Console.WriteLine("  --json               Output JSON");
    }

    private static int RunFlinkWithPreview(string[] args)
    {
        var configPath = GetOption(args, "--config") ?? "appsettings.json";
        var topicFilter = GetOption(args, "--topic");
        var kind = GetOption(args, "--kind") ?? "all";
        var asJson = HasFlag(args, "--json");
        var allowMissingTypes = HasFlag(args, "--allow-missing-types");
        var assemblyArg = GetOption(args, "--assembly");
        var hasAssembly = !string.IsNullOrWhiteSpace(assemblyArg);

        if (!string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(kind, "source", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(kind, "sink", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--kind must be 'all', 'source', or 'sink'.");
            return 2;
        }

        var cfg = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false)
            .Build();

        var options = cfg.GetKsqlDslOptions();
        var kafka = FlinkKafkaConnectorOptionsFactory.From(options);

        var result = new FlinkWithPreviewResult
        {
            Format = kafka.Format,
            BootstrapServers = kafka.BootstrapServers,
            SchemaRegistryUrl = kafka.SchemaRegistryUrl,
            GlobalWith = new Dictionary<string, string>(kafka.WithProperties, StringComparer.OrdinalIgnoreCase),
            SourceTopics = new(),
            SinkTopics = new(),
            Warnings = new(),
        };

        if (string.IsNullOrWhiteSpace(kafka.SchemaRegistryUrl))
            result.Warnings.Add($"'{FlinkKafkaConnectorOptions.SupportedFormat}' typically requires 'KsqlDsl:SchemaRegistry:Url'.");

        foreach (var topic in kafka.SourceWithByTopic.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(topicFilter) && !string.Equals(topic, topicFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kind, "sink", StringComparison.OrdinalIgnoreCase))
                continue;

            result.SourceTopics[topic] = BuildWithDictionary(kafka, topic, isSink: false);
        }

        foreach (var topic in kafka.SinkWithByTopic.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(topicFilter) && !string.Equals(topic, topicFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kind, "source", StringComparison.OrdinalIgnoreCase))
                continue;

            result.SinkTopics[topic] = BuildWithDictionary(kafka, topic, isSink: true);
        }

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine("-- Flink WITH preview (DDL)");
        if (!hasAssembly)
            Console.WriteLine("-- NOTE: no --assembly specified; column definitions are placeholders.");

        if (result.Warnings.Count > 0)
        {
            foreach (var w in result.Warnings)
                Console.WriteLine($"-- WARNING: {w}");
        }

        Console.WriteLine();

        var dialectProvider = new FlinkDialectProvider(kafka, (_, _) => Task.CompletedTask);
        var typeByTopic = LoadTypesByTopic(assemblyArg);

        var sourceDefinitions = new List<StreamingSourceDefinition>();
        var sourceSkeletons = new List<(string Topic, IReadOnlyDictionary<string, string> With)>();
        foreach (var (topic, dict) in result.SourceTopics.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!typeByTopic.TryGetValue(topic, out var type))
            {
                if (hasAssembly && !allowMissingTypes)
                    throw new InvalidOperationException($"Missing POCO for topic '{topic}'. Provide --assembly or use --allow-missing-types.");

                sourceSkeletons.Add((topic, dict));
                continue;
            }

            sourceDefinitions.Add(new StreamingSourceDefinition(
                type,
                topic,
                dialectProvider.NormalizeObjectName(topic),
                new StreamingSourceConfig(EventTime: null)));
        }

        foreach (var ddl in dialectProvider.GenerateSourceDdls(sourceDefinitions))
        {
            Console.WriteLine("-- source");
            Console.WriteLine(ddl);
            Console.WriteLine();
        }

        foreach (var (topic, dict) in sourceSkeletons)
        {
            Console.WriteLine(RenderCreateTableSkeleton(
                dialectProvider,
                topic,
                dict,
                header: $"-- source: {topic} (no POCO)"));
            Console.WriteLine();
        }

        var sinkDefinitions = new List<StreamingSinkDefinition>();
        var sinkSkeletons = new List<(string Topic, IReadOnlyDictionary<string, string> With)>();
        foreach (var (topic, dict) in result.SinkTopics.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!typeByTopic.TryGetValue(topic, out var type))
            {
                if (hasAssembly && !allowMissingTypes)
                    throw new InvalidOperationException($"Missing POCO for topic '{topic}'. Provide --assembly or use --allow-missing-types.");

                sinkSkeletons.Add((topic, dict));
                continue;
            }

            var pk = GetPrimaryKeyColumns(type);
            sinkDefinitions.Add(new StreamingSinkDefinition(
                type,
                topic,
                dialectProvider.NormalizeObjectName(topic),
                StreamingSinkMode.AppendOnly,
                pk));
        }

        foreach (var ddl in dialectProvider.GenerateSinkDdls(sinkDefinitions))
        {
            Console.WriteLine("-- sink");
            Console.WriteLine(ddl);
            Console.WriteLine();
        }

        foreach (var (topic, dict) in sinkSkeletons)
        {
            Console.WriteLine(RenderCreateTableSkeleton(
                dialectProvider,
                topic,
                dict,
                header: $"-- sink: {topic} (no POCO)"));
            Console.WriteLine();
        }

        return 0;
    }

    private static string RenderCreateTableSkeleton(
        FlinkDialectProvider dialectProvider,
        string topic,
        IReadOnlyDictionary<string, string> withProps,
        string header)
    {
        var objectName = dialectProvider.NormalizeObjectName(topic);
        var table = QuoteIdentifier(objectName);

        var with = string.Join(
            ",\n",
            withProps
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => $"  '{p.Key}' = '{EscapeSingleQuotes(p.Value)}'"));

        return string.Join(
            "\n",
            header,
            $"CREATE TABLE IF NOT EXISTS {table} (",
            "  -- TODO: columns",
            ") WITH (",
            with,
            ");");
    }

    private static Dictionary<string, Type> LoadTypesByTopic(string? assemblyArg)
    {
        if (string.IsNullOrWhiteSpace(assemblyArg))
            return new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        var paths = assemblyArg
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var asm = Assembly.LoadFrom(path);
            foreach (var type in asm.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;

                var topicAttr = type.GetCustomAttribute<KafkaTopicAttribute>(inherit: true);
                if (topicAttr is null)
                    continue;

                if (!result.TryAdd(topicAttr.Name, type))
                    throw new InvalidOperationException($"Duplicate KafkaTopic '{topicAttr.Name}' found in assemblies for types '{result[topicAttr.Name].FullName}' and '{type.FullName}'.");
            }
        }

        return result;
    }

    private static IReadOnlyList<string> GetPrimaryKeyColumns(Type entityType)
    {
        var keys = entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<KafkaKeyAttribute>(inherit: true) is not null)
            .Select(p => p.Name)
            .ToArray();

        return keys;
    }

    private static string QuoteIdentifier(string identifier)
    {
        var escaped = identifier.Replace("`", "``", StringComparison.Ordinal);
        return $"`{escaped}`";
    }

    private static string EscapeSingleQuotes(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static Dictionary<string, string> BuildWithDictionary(FlinkKafkaConnectorOptions kafka, string topicName, bool isSink)
    {
        if (!string.Equals(kafka.Format, FlinkKafkaConnectorOptions.SupportedFormat, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported Flink Kafka format '{kafka.Format}'. Only '{FlinkKafkaConnectorOptions.SupportedFormat}' is supported.");

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["connector"] = "kafka",
            ["topic"] = topicName,
            ["format"] = kafka.Format,
        };

        if (!string.IsNullOrWhiteSpace(kafka.BootstrapServers))
            props["properties.bootstrap.servers"] = kafka.BootstrapServers;

        if (!string.IsNullOrWhiteSpace(kafka.SchemaRegistryUrl))
            props["schema.registry.url"] = kafka.SchemaRegistryUrl;

        MergeWith(props, kafka.WithProperties, allowOverrideExisting: false);

        if (!isSink && kafka.SourceWithByTopic.TryGetValue(topicName, out var sourceProps))
            MergeWith(props, sourceProps, allowOverrideExisting: true);

        if (isSink && kafka.SinkWithByTopic.TryGetValue(topicName, out var sinkProps))
            MergeWith(props, sinkProps, allowOverrideExisting: true);

        MergeWith(props, kafka.AdditionalProperties, allowOverrideExisting: false);

        ValidateFlinkKafkaWith(props, isSink);

        return props;
    }

    private static void MergeWith(Dictionary<string, string> target, IEnumerable<KeyValuePair<string, string>> additions, bool allowOverrideExisting)
    {
        foreach (var (k, v) in additions)
        {
            if (string.IsNullOrWhiteSpace(k))
                continue;

            if (IsProtectedWithKey(k))
                throw new InvalidOperationException($"WITH property '{k}' is reserved and cannot be overridden.");

            if (target.TryAdd(k, v))
                continue;

            if (string.Equals(target[k], v, StringComparison.Ordinal))
                continue;

            if (!allowOverrideExisting)
                throw new InvalidOperationException($"Duplicate WITH property key detected: '{k}'.");

            target[k] = v;
        }
    }

    private static bool IsProtectedWithKey(string key)
    {
        return string.Equals(key, "connector", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "topic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "format", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "properties.bootstrap.servers", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "schema.registry.url", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateFlinkKafkaWith(IReadOnlyDictionary<string, string> props, bool isSink)
    {
        if (isSink)
            return;

        if (!props.TryGetValue("scan.startup.mode", out var mode) || string.IsNullOrWhiteSpace(mode))
            return;

        mode = mode.Trim();

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "earliest-offset",
            "latest-offset",
            "group-offsets",
            "timestamp",
            "specific-offsets",
        };

        if (!allowed.Contains(mode))
            throw new InvalidOperationException($"Unsupported scan.startup.mode '{mode}'. Allowed: earliest-offset, latest-offset, group-offsets, timestamp, specific-offsets.");

        if (string.Equals(mode, "timestamp", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue("scan.startup.timestamp-millis", out var millis) || string.IsNullOrWhiteSpace(millis))
                throw new InvalidOperationException("scan.startup.mode=timestamp requires 'scan.startup.timestamp-millis'.");

            if (!long.TryParse(millis, out var _))
                throw new InvalidOperationException("scan.startup.timestamp-millis must be an integer (milliseconds since epoch).");
        }

        if (string.Equals(mode, "specific-offsets", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue("scan.startup.specific-offsets", out var offsets) || string.IsNullOrWhiteSpace(offsets))
                throw new InvalidOperationException("scan.startup.mode=specific-offsets requires 'scan.startup.specific-offsets'.");
        }
    }

    private sealed class FlinkWithPreviewResult
    {
        public string Format { get; init; } = "";
        public string BootstrapServers { get; init; } = "";
        public string SchemaRegistryUrl { get; init; } = "";
        public Dictionary<string, string> GlobalWith { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, string>> SourceTopics { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, string>> SinkTopics { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Warnings { get; init; } = new();
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
