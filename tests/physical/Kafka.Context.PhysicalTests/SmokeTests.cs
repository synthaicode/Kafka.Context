using Confluent.Kafka;
using Kafka.Context.Attributes;
using Kafka.Context.Abstractions;
using Kafka.Context.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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

            var gotOrder = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var orderCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var orderConsumeTask = ctx.Orders.ForEachAsync((o, headers, _) =>
            {
                Assert.True(headers.TryGetValue("traceId", out var traceId));
                Assert.Equal("t-1", traceId);
                gotOrder.TrySetResult(true);
                orderCts.Cancel();
                return Task.CompletedTask;
            }, autoCommit: true, orderCts.Token);

            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
            await ctx.Orders.AddAsync(
                new PhysicalOrder { Id = 1, Amount = 10.5m },
                new Dictionary<string, string> { ["traceId"] = "t-1" },
                CancellationToken.None);

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
            await ctx.Orders.AddAsync(
                new PhysicalOrder { Id = 2, Amount = -1m },
                new Dictionary<string, string> { ["traceId"] = "t-2" },
                cts2.Token);

            await Task.WhenAny(gotDlq.Task, Task.Delay(TimeSpan.FromSeconds(20), CancellationToken.None));
            Assert.True(gotDlq.Task.IsCompletedSuccessfully);

            var envelope = await gotDlq.Task;
            Assert.Equal(topic, envelope.Topic);
            Assert.False(string.IsNullOrWhiteSpace(envelope.ErrorType));
            Assert.False(string.IsNullOrWhiteSpace(envelope.ErrorFingerprint));
            Assert.True(envelope.Headers.TryGetValue("traceId", out var dlqTraceId));
            Assert.Equal("t-2", dlqTraceId);

            try
            {
                await Task.WhenAll(dlqConsumeTask, failingConsumeTask).WaitAsync(TimeSpan.FromSeconds(40));
            }
            finally
            {
                cts2.Cancel();
            }
        });
    }

    [Fact]
    public async Task ManualCommit_Restart_ResumesFromCommittedOffset()
    {
        if (!IsEnabled())
            return;

        await RunWithRetryAsync(async () =>
        {
            var baseClientId = $"physical-mc-{Guid.NewGuid():N}";
            var groupId = $"physical-mc-group-{Guid.NewGuid():N}";

            IConfiguration BuildConfig(string clientId) =>
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["KsqlDsl:Common:BootstrapServers"] = "127.0.0.1:39092",
                        ["KsqlDsl:Common:ClientId"] = clientId,
                        ["KsqlDsl:SchemaRegistry:Url"] = "http://127.0.0.1:18081",
                        ["KsqlDsl:DlqTopicName"] = $"dead_letter_queue_{clientId}",
                        ["KsqlDsl:Topics:physical_manual_commit_orders:Consumer:GroupId"] = groupId,
                        ["KsqlDsl:Topics:physical_manual_commit_orders:Consumer:AutoOffsetReset"] = "Earliest",
                    })
                    .Build();

            // First run: consume 1 record and commit it.
            await using (var ctx1 = new PhysicalManualCommitContext(BuildConfig($"{baseClientId}-1"), LoggerFactory.Create(b => b.AddConsole())))
            {
                using var provisionCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await ctx1.ProvisionAsync(provisionCts.Token);

                await ctx1.ManualOrders.AddAsync(
                    new ManualCommitOrder { OrderId = 1, Amount = 10m },
                    new Dictionary<string, string> { ["traceId"] = "mc-1" },
                    CancellationToken.None);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var gotFirst = new TaskCompletionSource<MessageMeta>(TaskCreationOptions.RunContinuationsAsynchronously);

                var t1 = ctx1.ManualOrders.ForEachAsync((order, headers, meta) =>
                {
                    if (order.OrderId == 1)
                    {
                        Assert.Equal("mc-1", headers.GetValueOrDefault("traceId"));
                        ctx1.ManualOrders.Commit(order);
                        gotFirst.TrySetResult(meta);
                        cts.Cancel();
                    }

                    return Task.CompletedTask;
                }, autoCommit: false, cts.Token);

                var completed = await Task.WhenAny(gotFirst.Task, Task.Delay(TimeSpan.FromSeconds(20), CancellationToken.None));
                Assert.Equal(gotFirst.Task, completed);
                await t1;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);

            // Second run: ensure we don't re-consume the committed record.
            await using (var ctx2 = new PhysicalManualCommitContext(BuildConfig($"{baseClientId}-2"), LoggerFactory.Create(b => b.AddConsole())))
            {
                using var provisionCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await ctx2.ProvisionAsync(provisionCts.Token);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var gotSecond = new TaskCompletionSource<MessageMeta>(TaskCreationOptions.RunContinuationsAsynchronously);

                var seenIds = new List<int>();
                var t2 = ctx2.ManualOrders.ForEachAsync((order, headers, meta) =>
                {
                    seenIds.Add(order.OrderId);

                    if (order.OrderId == 1)
                        throw new InvalidOperationException("Re-consumed committed record (OrderId=1).");

                    if (order.OrderId == 2)
                    {
                        Assert.Equal("mc-2", headers.GetValueOrDefault("traceId"));
                        ctx2.ManualOrders.Commit(order);
                        gotSecond.TrySetResult(meta);
                        cts.Cancel();
                    }

                    return Task.CompletedTask;
                }, autoCommit: false, cts.Token);

                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
                await ctx2.ManualOrders.AddAsync(
                    new ManualCommitOrder { OrderId = 2, Amount = 20m },
                    new Dictionary<string, string> { ["traceId"] = "mc-2" },
                    CancellationToken.None);

                var completed = await Task.WhenAny(gotSecond.Task, Task.Delay(TimeSpan.FromSeconds(20), CancellationToken.None));
                Assert.Equal(gotSecond.Task, completed);
                await t2;
            }
        });
    }

    [Fact]
    public async Task Add_Consume_AcrossProcesses_SchemaRegistry_Works()
    {
        if (!IsEnabled())
            return;

        await RunWithRetryAsync(async () =>
        {
            var repoRoot = FindRepoRoot();
            var runnerProject = Path.Combine(repoRoot, "tests", "physical", "Kafka.Context.PhysicalRunner", "Kafka.Context.PhysicalRunner.csproj");
            if (!File.Exists(runnerProject))
                throw new FileNotFoundException($"Runner project not found: {runnerProject}");

            var runnerDll = Path.Combine(repoRoot, "tests", "physical", "Kafka.Context.PhysicalRunner", "bin", "Release", "net8.0", "Kafka.Context.PhysicalRunner.dll");
            var buildExit = await RunDotnetBuildAsync(repoRoot, runnerProject, timeout: TimeSpan.FromMinutes(5));
            if (buildExit != 0 || !File.Exists(runnerDll))
                throw new InvalidOperationException($"Runner build failed (exit={buildExit}). Expected dll: {runnerDll}");

            var baseClientId = $"physical-proc-{Guid.NewGuid():N}";
            var orderId = Random.Shared.Next(10_000, 99_999);

            var commonEnv = new Dictionary<string, string>
            {
                ["KAFKA_CONTEXT_BOOTSTRAP_SERVERS"] = "127.0.0.1:39092",
                ["KAFKA_CONTEXT_SCHEMA_REGISTRY_URL"] = "http://127.0.0.1:18081",
                ["KAFKA_CONTEXT_ORDER_ID"] = orderId.ToString(),
            };

            using var consumer = StartRunnerProcess(repoRoot, runnerDll, "consume", $"{baseClientId}-consumer", commonEnv);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);

                using var producer = StartRunnerProcess(repoRoot, runnerDll, "produce", $"{baseClientId}-producer", commonEnv);

                var producerExit = await WaitForExitAsync(producer, TimeSpan.FromSeconds(60));
                if (producerExit != 0)
                    throw new InvalidOperationException($"Producer failed (exit={producerExit}).\nSTDOUT:\n{producer.StandardOutput.ReadToEnd()}\nSTDERR:\n{producer.StandardError.ReadToEnd()}");

                var consumerExit = await WaitForExitAsync(consumer, TimeSpan.FromSeconds(60));
                if (consumerExit != 0)
                    throw new InvalidOperationException($"Consumer failed (exit={consumerExit}).\nSTDOUT:\n{consumer.StandardOutput.ReadToEnd()}\nSTDERR:\n{consumer.StandardError.ReadToEnd()}");
            }
            finally
            {
                try { if (!consumer.HasExited) consumer.Kill(entireProcessTree: true); } catch { }
            }
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

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Kafka.Context.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found (Kafka.Context.sln/.git).");
    }

    private static async Task<int> RunDotnetBuildAsync(string repoRoot, string projectPath, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build -c Release \"{projectPath}\"",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet build");
        var exit = await WaitForExitAsync(p, timeout);
        if (exit != 0)
            throw new InvalidOperationException($"dotnet build failed (exit={exit}).\nSTDOUT:\n{p.StandardOutput.ReadToEnd()}\nSTDERR:\n{p.StandardError.ReadToEnd()}");
        return exit;
    }

    private static Process StartRunnerProcess(string repoRoot, string runnerDll, string mode, string clientId, IReadOnlyDictionary<string, string> env)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{runnerDll}\" {mode}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.Environment["KAFKA_CONTEXT_CLIENT_ID"] = clientId;
        foreach (var kv in env)
            psi.Environment[kv.Key] = kv.Value;

        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start runner process");
        return p;
    }

    private static async Task<int> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        }

        if (!process.HasExited)
            throw new TimeoutException("Process did not exit in time.");

        return process.ExitCode;
    }

    [KafkaTopic("physical_orders_physical")]
    private sealed class PhysicalOrder
    {
        public int Id { get; set; }
        [KafkaDecimal(9, 2)]
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

    [KafkaTopic("physical_manual_commit_orders")]
    private sealed class ManualCommitOrder
    {
        public int OrderId { get; set; }
        [KafkaDecimal(9, 2)]
        public decimal Amount { get; set; }
    }

    private sealed class PhysicalManualCommitContext : KafkaContext
    {
        public PhysicalManualCommitContext(IConfiguration configuration, ILoggerFactory loggerFactory)
            : base(configuration, loggerFactory) { }

        public EventSet<ManualCommitOrder> ManualOrders { get; set; } = null!;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ManualCommitOrder>();
        }
    }
}
