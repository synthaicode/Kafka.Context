using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;
using Microsoft.Extensions.Configuration;

namespace Kafka.Context.Tests;

[Trait("Level", "L4")]
public class StreamingProvisioningTests
{
    [Fact]
    public async Task ProvisionStreamingAsync_CTAS_DuplicateObjectName_FailsFastBeforeExecution()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new CtasDuplicateContext(dialect);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.ProvisionStreamingAsync());
        Assert.Contains("Duplicate CTAS object name", ex.Message);
        Assert.Empty(dialect.ExecutedDdls);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_INSERT_Duplicates_ExecuteInDeclarationOrder()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new InsertDuplicateContext(dialect);

        await ctx.ProvisionStreamingAsync();

        Assert.Equal(2, dialect.ExecutedDdls.Count);
        Assert.Contains("source:InputA", dialect.ExecutedDdls[0]);
        Assert.Contains("source:InputB", dialect.ExecutedDdls[1]);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_LoopGuard_DetectsJoinSourceMatchesOutputTopic()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new LoopJoinContext(dialect);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.ProvisionStreamingAsync());
        Assert.Contains("Streaming loop detected", ex.Message);
        Assert.Empty(dialect.ExecutedDdls);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_Join_StreamStream_IsRejected()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new StreamStreamJoinContext(dialect);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.ProvisionStreamingAsync());
        Assert.Contains("CTAS-derived source", ex.Message);
        Assert.Empty(dialect.ExecutedDdls);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_Join_KeyEqualityOnly_IsEnforced()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new NonKeyJoinContext(dialect);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.ProvisionStreamingAsync());
        Assert.Contains("key-equality", ex.Message);
        Assert.Empty(dialect.ExecutedDdls);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_Join_WithSingleCtasSide_IsAllowed()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new CtasJoinContext(dialect);

        await ctx.ProvisionStreamingAsync();

        Assert.Equal(2, dialect.ExecutedDdls.Count);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_Window_RequiresCtasAndWindowBoundsInGroupBy()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new InvalidWindowContext(dialect);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.ProvisionStreamingAsync());
        Assert.Contains("Window GroupBy", ex.Message);
        Assert.Empty(dialect.ExecutedDdls);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_Window_Tumble_IsAllowed()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new ValidWindowContext(dialect);

        await ctx.ProvisionStreamingAsync();
        Assert.Single(dialect.ExecutedDdls);
    }

    [Fact]
    public async Task ProvisionStreamingAsync_OutputModeFinal_RequiresWindow()
    {
        var dialect = new RecordingDialectProvider();
        await using var ctx = new FinalWithoutWindowContext(dialect);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.ProvisionStreamingAsync());
        Assert.Contains("OutputMode.Final", ex.Message);
        Assert.Empty(dialect.ExecutedDdls);
    }

    private sealed class RecordingDialectProvider : IStreamingDialectProvider
    {
        public List<string> ExecutedDdls { get; } = new();

        public string NormalizeObjectName(string suggestedObjectName) => suggestedObjectName;

        public string GenerateDdl(StreamingQueryPlan plan, StreamingStatementKind kind, StreamingOutputMode outputMode, string objectName, string outputTopic)
        {
            var firstSource = plan.SourceTypes.Count > 0 ? plan.SourceTypes[0].Name : "(none)";
            return $"{kind}:{outputMode}:{objectName}:{outputTopic}:source:{firstSource}";
        }

        public Task ExecuteAsync(string ddl, CancellationToken cancellationToken)
        {
            ExecutedDdls.Add(ddl);
            return Task.CompletedTask;
        }
    }

    private abstract class StreamingTestContextBase : KafkaContext
    {
        private readonly IStreamingDialectProvider _dialectProvider;

        protected StreamingTestContextBase(IStreamingDialectProvider dialectProvider)
            : base(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build())
        {
            _dialectProvider = dialectProvider;
        }

        protected override IStreamingDialectProvider? ResolveStreamingDialectProvider() => _dialectProvider;
    }

    private sealed class CtasDuplicateContext : StreamingTestContextBase
    {
        public CtasDuplicateContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutputSame1>().ToQuery(q => q
                .From<InputA>()
                .GroupBy(x => x.Id)
                .Select(x => new OutputSame1()));

            modelBuilder.Entity<OutputSame2>().ToQuery(q => q
                .From<InputB>()
                .GroupBy(x => x.Id)
                .Select(x => new OutputSame2()));
        }
    }

    private sealed class InsertDuplicateContext : StreamingTestContextBase
    {
        public InsertDuplicateContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InsertOut1>().ToQuery(q => q
                .From<InputA>()
                .Select(x => new InsertOut1()));

            modelBuilder.Entity<InsertOut2>().ToQuery(q => q
                .From<InputB>()
                .Select(x => new InsertOut2()));
        }
    }

    private sealed class LoopJoinContext : StreamingTestContextBase
    {
        public LoopJoinContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LoopOut>().ToQuery(q => q
                .From<InputA>()
                .Join<InputA, InputB>((a, b) => true)
                .Select((a, b) => new LoopOut()));
        }
    }

    private sealed class StreamStreamJoinContext : StreamingTestContextBase
    {
        public StreamStreamJoinContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JoinOut>().ToQuery(q => q
                .From<InputA>()
                .Join<InputA, InputB>((a, b) => a.Id == b.Id)
                .Select((a, b) => new JoinOut()));
        }
    }

    private sealed class NonKeyJoinContext : StreamingTestContextBase
    {
        public NonKeyJoinContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NonKeyAgg>().ToQuery(q => q
                .From<InputA>()
                .GroupBy(x => x.Id)
                .Select(x => new NonKeyAgg { Id = x.Id }));

            modelBuilder.Entity<NonKeyOut>().ToQuery(q => q
                .From<NonKeyAgg>()
                .Join<NonKeyAgg, InputB>((a, b) => true)
                .Select((a, b) => new NonKeyOut()));
        }
    }

    private sealed class CtasJoinContext : StreamingTestContextBase
    {
        public CtasJoinContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AggById>().ToQuery(q => q
                .From<InputA>()
                .GroupBy(x => x.Id)
                .Select(x => new AggById { Id = x.Id }));

            modelBuilder.Entity<JoinOut>().ToQuery(q => q
                .From<AggById>()
                .Join<AggById, InputB>((a, b) => a.Id == b.Id)
                .Select((a, b) => new JoinOut()));
        }
    }

    private sealed class InvalidWindowContext : StreamingTestContextBase
    {
        public InvalidWindowContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WindowOut>().ToQuery(q => q
                .From<WindowIn>()
                .TumbleWindow(x => x.EventTime, TimeSpan.FromSeconds(5))
                .GroupBy(x => x.Id)
                .Select(x => new WindowOut()));
        }
    }

    private sealed class ValidWindowContext : StreamingTestContextBase
    {
        public ValidWindowContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WindowOut>().ToQuery(q => q
                .From<WindowIn>()
                .TumbleWindow(x => x.EventTime, TimeSpan.FromSeconds(5))
                .GroupBy(x => new { x.Id, W1 = FlinkWindow.Start(), W2 = FlinkWindow.End() })
                .Select(x => new WindowOut()));
        }
    }

    private sealed class FinalWithoutWindowContext : StreamingTestContextBase
    {
        public FinalWithoutWindowContext(IStreamingDialectProvider dialectProvider) : base(dialectProvider) { }

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FinalOut>().ToQuery(q => q
                .From<InputA>()
                .Select(x => new FinalOut()), outputMode: StreamingOutputMode.Final);
        }
    }

    [KafkaTopic("input-a")]
    private sealed class InputA
    {
        public int Id { get; set; }
    }

    [KafkaTopic("input-b")]
    private sealed class InputB
    {
        public int Id { get; set; }
    }

    [KafkaTopic("same-output")]
    private sealed class OutputSame1 { }

    [KafkaTopic("same-output")]
    private sealed class OutputSame2 { }

    [KafkaTopic("dup-output")]
    private sealed class InsertOut1 { }

    [KafkaTopic("dup-output")]
    private sealed class InsertOut2 { }

    [KafkaTopic("input-b")]
    private sealed class LoopOut { }

    [KafkaTopic("agg-by-id")]
    private sealed class AggById
    {
        public int Id { get; set; }
    }

    [KafkaTopic("nonkey-agg")]
    private sealed class NonKeyAgg
    {
        public int Id { get; set; }
    }

    [KafkaTopic("join-out")]
    private sealed class JoinOut { }

    [KafkaTopic("nonkey-out")]
    private sealed class NonKeyOut { }

    [KafkaTopic("window-in")]
    private sealed class WindowIn
    {
        public int Id { get; set; }
        public DateTime EventTime { get; set; }
    }

    [KafkaTopic("window-out")]
    private sealed class WindowOut { }

    [KafkaTopic("final-out")]
    private sealed class FinalOut { }
}
