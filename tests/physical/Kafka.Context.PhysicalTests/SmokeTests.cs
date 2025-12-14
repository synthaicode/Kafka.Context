using Confluent.Kafka;
using Kafka.Context.Attributes;
using Kafka.Context.Abstractions;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kafka.Context.PhysicalTests;

public sealed class SmokeTests
{
    [Fact]
    public async Task Provision_Add_Consume_And_Dlq()
    {
        if (!IsEnabled())
            return;

        await RunWithRetryAsync(async () =>
        {
            var topic = "physical_orders_physical";
            var clientId = $"physical-{Guid.NewGuid():N}";
            var dlqTopic = $"dead_letter_queue_{clientId}";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["KsqlDsl:Common:BootstrapServers"] = "127.0.0.1:39092",
                    ["KsqlDsl:Common:ClientId"] = clientId,
                    ["KsqlDsl:SchemaRegistry:Url"] = "http://127.0.0.1:18081",
                    ["KsqlDsl:DlqTopicName"] = dlqTopic,
                })
                .Build();

            await using var ctx = new PhysicalTestContext(config, LoggerFactory.Create(b => b.AddConsole()));

            using var provisionCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await ctx.ProvisionAsync(provisionCts.Token);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var gotOrder = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var orderConsumeTask = ctx.Orders.ForEachAsync(o =>
            {
                gotOrder.TrySetResult(true);
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token);

            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
            await ctx.Orders.AddAsync(new PhysicalOrder { Id = 1, Amount = 10.5m }, cts.Token);

            await Task.WhenAny(gotOrder.Task, Task.Delay(TimeSpan.FromSeconds(20), CancellationToken.None));
            Assert.True(gotOrder.Task.IsCompletedSuccessfully);
            await orderConsumeTask;

            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var gotDlq = new TaskCompletionSource<DlqEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

            var dlqConsumeTask = ctx.Dlq.ForEachAsync((env, _, _) =>
            {
                if (env.Topic == topic)
                {
                    gotDlq.TrySetResult(env);
                    cts2.Cancel();
                }
                return Task.CompletedTask;
            }, cts2.Token);

            var failingConsumeTask = ctx.Orders
                .WithRetry(maxRetries: 2, retryInterval: TimeSpan.FromMilliseconds(200))
                .OnError(ErrorAction.DLQ)
                .ForEachAsync(_ => throw new InvalidOperationException("boom"), cts2.Token);

            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
            await ctx.Orders.AddAsync(new PhysicalOrder { Id = 2, Amount = -1m }, cts2.Token);

            await Task.WhenAny(gotDlq.Task, Task.Delay(TimeSpan.FromSeconds(20), CancellationToken.None));
            Assert.True(gotDlq.Task.IsCompletedSuccessfully);

            var envelope = await gotDlq.Task;
            Assert.Equal(topic, envelope.Topic);
            Assert.False(string.IsNullOrWhiteSpace(envelope.ErrorType));
            Assert.False(string.IsNullOrWhiteSpace(envelope.ErrorFingerprint));

            await Task.WhenAll(dlqConsumeTask, failingConsumeTask);
        });
    }

    private static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable("KAFKA_CONTEXT_PHYSICAL"), "1", StringComparison.Ordinal);

    private static async Task RunWithRetryAsync(Func<Task> action, int maxAttempts = 3)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (maxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attempt)));
                await Task.Delay(delay, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is OperationCanceledException)
                return true;
            if (cur is HttpRequestException)
                return true;
            if (cur is KafkaException)
                return true;
        }

        return false;
    }

    [KsqlTopic("physical_orders_physical")]
    private sealed class PhysicalOrder
    {
        public int Id { get; set; }
        [KsqlDecimal(9, 2)]
        public decimal Amount { get; set; }
    }

    private sealed class PhysicalTestContext : KafkaContext
    {
        public PhysicalTestContext(IConfiguration configuration, ILoggerFactory loggerFactory)
            : base(configuration, loggerFactory) { }

        public EventSet<PhysicalOrder> Orders { get; set; } = null!;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PhysicalOrder>();
        }
    }
}
