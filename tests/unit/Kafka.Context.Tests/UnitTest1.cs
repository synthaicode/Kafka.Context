using Kafka.Context.Configuration;
using Microsoft.Extensions.Configuration;

namespace Kafka.Context.Tests;

public class UnitTest1
{
    [Fact]
    public void WithRetry_DefaultInterval_Is1Second()
    {
        var ctx = new DummyContext();
        var set = ctx.Set<DummyEntity>().WithRetry(3);
        Assert.Equal(TimeSpan.FromSeconds(1), set.Policy.RetryInterval);
    }

    [Fact]
    public void KsqlDslOptions_Binds_MonitoringKeys()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KsqlDsl:SchemaRegistry:Monitoring:SubjectWaitTimeout"] = "00:00:30",
                ["KsqlDsl:SchemaRegistry:Monitoring:SubjectWaitMaxAttempts"] = "7",
            })
            .Build();

        var options = config.GetKsqlDslOptions();
        Assert.Equal(TimeSpan.FromSeconds(30), options.SchemaRegistry.Monitoring.SubjectWaitTimeout);
        Assert.Equal(7, options.SchemaRegistry.Monitoring.SubjectWaitMaxAttempts);
    }

    private sealed class DummyContext : KafkaContext
    {
        public DummyContext()
            : base(new ConfigurationBuilder().AddInMemoryCollection().Build())
        {
        }
    }

    [Kafka.Context.Attributes.KafkaTopic("dummy")]
    private sealed class DummyEntity;
}
