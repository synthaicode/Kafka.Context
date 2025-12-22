using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;

namespace Kafka.Context.Tests;

[Trait("Level", "L2")]
public class FlinkDialectProviderRenderingTests
{
    [Fact]
    public void GenerateDdl_RendersCommonStringShapes()
    {
        var dialect = new FlinkDialectProvider((_, _) => Task.CompletedTask);

        var query = new StreamingQueryBuilder()
            .From<Input>()
            .Where(x => x.Name.Length > 3 && x.Name.Contains("a_b%"))
            .Select(x => new Output
            {
                Sub = x.Name.Substring(1, 2),
                Pos = x.Name.IndexOf("x"),
        });

        var plan = query.Plan with { SourceTopics = new[] { "input-a" } };
        var ddl = dialect.GenerateDdl(plan, StreamingStatementKind.StreamInsert, StreamingOutputMode.Changelog, objectName: "out-topic", outputTopic: "out-topic");

        Assert.Contains("CHAR_LENGTH", ddl);
        Assert.Contains("SUBSTRING", ddl);
        Assert.Contains("POSITION", ddl);
        Assert.Contains("ESCAPE '^'", ddl);
        Assert.Contains("a^_b^%", ddl);
    }

    [Fact]
    public void GenerateDdl_StringSplit_IsRejected()
    {
        var dialect = new FlinkDialectProvider((_, _) => Task.CompletedTask);

        var query = new StreamingQueryBuilder()
            .From<Input>()
            .Where(x => x.Name.Split(',') != null)
            .Select(x => new Output { Sub = x.Name });

        var plan = query.Plan with { SourceTopics = new[] { "input-a" } };

        Assert.Throws<NotSupportedException>(() =>
            dialect.GenerateDdl(plan, StreamingStatementKind.StreamInsert, StreamingOutputMode.Changelog, objectName: "out-topic", outputTopic: "out-topic"));
    }

    [Fact]
    public void GenerateDdl_TumbleWindow_RendersTumbleTvp()
    {
        var dialect = new FlinkDialectProvider((_, _) => Task.CompletedTask);

        var query = new StreamingQueryBuilder()
            .From<WindowInput>()
            .TumbleWindow(x => x.EventTime, TimeSpan.FromSeconds(5))
            .GroupBy(x => new { x.CustomerId, WindowStart = FlinkWindow.Start() })
            .Select(x => new WindowOutput
            {
                CustomerId = x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                Cnt = FlinkAgg.Count(),
                Total = FlinkAgg.Sum(x.CustomerId),
        });

        var plan = query.Plan with { SourceTopics = new[] { "window-input" } };
        var ddl = dialect.GenerateDdl(plan, StreamingStatementKind.TableCtas, StreamingOutputMode.Changelog, objectName: "window-output", outputTopic: "window-output");

        Assert.Contains("TUMBLE", ddl);
        Assert.Contains("DESCRIPTOR(`eventtime`)", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("window_start", ddl);
        Assert.Contains("COUNT(*)", ddl);
        Assert.Contains("SUM(", ddl);
        Assert.Contains("GROUP BY", ddl);
    }

    [Fact]
    public void GenerateDdl_HavingOnProjection_IsInlined()
    {
        var dialect = new FlinkDialectProvider((_, _) => Task.CompletedTask);

        var query = new StreamingQueryBuilder()
            .From<WindowInput>()
            .TumbleWindow(x => x.EventTime, TimeSpan.FromSeconds(5))
            .GroupBy(x => new { x.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(x => new WindowAgg
            {
                CustomerId = x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                TotalAmount = FlinkAgg.Sum(x.CustomerId),
            })
            .Having(x => x.TotalAmount > 10);

        var plan = query.Plan with { SourceTopics = new[] { "window-input" } };
        var ddl = dialect.GenerateDdl(plan, StreamingStatementKind.TableCtas, StreamingOutputMode.Changelog, objectName: "window-agg", outputTopic: "window-agg");

        Assert.Contains(" HAVING ", ddl);
        Assert.Contains("SUM", ddl);
        Assert.DoesNotContain("t0.totalamount", ddl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateDdl_UpsertSink_AddsInformationalComment()
    {
        var dialect = new FlinkDialectProvider((_, _) => Task.CompletedTask);

        var query = new StreamingQueryBuilder()
            .From<WindowInput>()
            .TumbleWindow(x => x.EventTime, TimeSpan.FromSeconds(5))
            .GroupBy(x => new { x.CustomerId, WindowStart = FlinkWindow.Start() })
            .Select(x => new WindowOutput
            {
                CustomerId = x.CustomerId,
                WindowStart = FlinkWindow.Start(),
                Cnt = FlinkAgg.Count(),
                Total = FlinkAgg.Sum(x.CustomerId),
            });

        var plan = query.Plan with { SourceTopics = new[] { "window-input" }, SinkMode = StreamingSinkMode.Upsert };
        var ddl = dialect.GenerateDdl(plan, StreamingStatementKind.TableCtas, StreamingOutputMode.Changelog, objectName: "window-output", outputTopic: "window-output");

        Assert.StartsWith("-- sink: upsert", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateSourceDdls_NonAvroFormat_IsRejected()
    {
        var opts = new FlinkKafkaConnectorOptions { Format = "json" };
        var dialect = new FlinkDialectProvider(opts, (_, _) => Task.CompletedTask);

        var sources = new[]
        {
            new StreamingSourceDefinition(typeof(Input), "input-a", "input-a", new StreamingSourceConfig(EventTime: null)),
        };

        Assert.Throws<InvalidOperationException>(() => dialect.GenerateSourceDdls(sources));
    }

    [Fact]
    public void GenerateSourceDdls_ScanStartupMode_InvalidValue_IsRejected()
    {
        var opts = new FlinkKafkaConnectorOptions();
        opts.WithProperties["scan.startup.mode"] = "nope";

        var dialect = new FlinkDialectProvider(opts, (_, _) => Task.CompletedTask);

        var sources = new[]
        {
            new StreamingSourceDefinition(typeof(Input), "input-a", "input-a", new StreamingSourceConfig(EventTime: null)),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => dialect.GenerateSourceDdls(sources));
        Assert.Contains("scan.startup.mode", ex.Message);
    }

    [Fact]
    public void GenerateSourceDdls_ScanStartupMode_Timestamp_RequiresMillis()
    {
        var opts = new FlinkKafkaConnectorOptions();
        opts.WithProperties["scan.startup.mode"] = "timestamp";

        var dialect = new FlinkDialectProvider(opts, (_, _) => Task.CompletedTask);

        var sources = new[]
        {
            new StreamingSourceDefinition(typeof(Input), "input-a", "input-a", new StreamingSourceConfig(EventTime: null)),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => dialect.GenerateSourceDdls(sources));
        Assert.Contains("timestamp-millis", ex.Message);
    }

    [Fact]
    public void GenerateSourceDdls_PerTopicWith_IsApplied()
    {
        var opts = new FlinkKafkaConnectorOptions();
        opts.WithProperties["scan.startup.mode"] = "group-offsets";
        opts.SourceWithByTopic["input-a"] = new Dictionary<string, string>
        {
            ["scan.startup.mode"] = "earliest-offset",
        };

        var dialect = new FlinkDialectProvider(opts, (_, _) => Task.CompletedTask);

        var sources = new[]
        {
            new StreamingSourceDefinition(typeof(Input), "input-a", "input-a", new StreamingSourceConfig(EventTime: null)),
        };

        var ddls = dialect.GenerateSourceDdls(sources);
        Assert.Single(ddls);
        Assert.Contains("'scan.startup.mode' = 'earliest-offset'", ddls[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateSourceDdls_CommonAdditionalProperties_Duplicate_IsRejected()
    {
        var opts = new FlinkKafkaConnectorOptions();
        opts.WithProperties["scan.startup.mode"] = "group-offsets";
        opts.AdditionalProperties["scan.startup.mode"] = "earliest-offset";

        var dialect = new FlinkDialectProvider(opts, (_, _) => Task.CompletedTask);

        var sources = new[]
        {
            new StreamingSourceDefinition(typeof(Input), "input-a", "input-a", new StreamingSourceConfig(EventTime: null)),
        };

        Assert.Throws<InvalidOperationException>(() => dialect.GenerateSourceDdls(sources));
    }

    [Fact]
    public void FlinkKafkaConnectorOptionsFactory_ScanStartupMode_OverridesGlobal()
    {
        var config = new Kafka.Context.Configuration.KsqlDslOptions
        {
            Streaming = new Kafka.Context.Configuration.StreamingOptions
            {
                Flink = new Kafka.Context.Configuration.FlinkStreamingOptions
                {
                    ScanStartupMode = "group-offsets",
                }
            }
        };

        var kafka = FlinkKafkaConnectorOptionsFactory.From(config);
        Assert.Equal("group-offsets", kafka.WithProperties["scan.startup.mode"]);
    }

    [Fact]
    public void FlinkKafkaConnectorOptionsFactory_SourceGroupIdByTopic_IsMapped()
    {
        var config = new Kafka.Context.Configuration.KsqlDslOptions
        {
            Streaming = new Kafka.Context.Configuration.StreamingOptions
            {
                Flink = new Kafka.Context.Configuration.FlinkStreamingOptions
                {
                    SourceGroupIdByTopic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["orders"] = "orders-consumer",
                    }
                }
            }
        };

        var kafka = FlinkKafkaConnectorOptionsFactory.From(config);
        Assert.Equal("orders-consumer", kafka.SourceWithByTopic["orders"]["properties.group.id"]);
    }

    [Kafka.Context.Attributes.KafkaTopic("input-a")]
    private sealed class Input
    {
        public string Name { get; set; } = "";
    }

    [Kafka.Context.Attributes.KafkaTopic("out-topic")]
    private sealed class Output
    {
        public string Sub { get; set; } = "";
        public int Pos { get; set; }
    }

    [Kafka.Context.Attributes.KafkaTopic("window-input")]
    private sealed class WindowInput
    {
        public int CustomerId { get; set; }
        public DateTime EventTime { get; set; }
    }

    [Kafka.Context.Attributes.KafkaTopic("window-output")]
    private sealed class WindowOutput
    {
        public int CustomerId { get; set; }
        public object WindowStart { get; set; } = null!;
        public long Cnt { get; set; }
        public int Total { get; set; }
    }

    [Kafka.Context.Attributes.KafkaTopic("window-agg")]
    private sealed class WindowAgg
    {
        public int CustomerId { get; set; }
        public object WindowStart { get; set; } = null!;
        public object WindowEnd { get; set; } = null!;
        public int TotalAmount { get; set; }
    }
}
