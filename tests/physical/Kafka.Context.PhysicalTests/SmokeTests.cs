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
