using Kafka.Context;
using Kafka.Context.Abstractions;
using Kafka.Context.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kafka.Context.Tests;

[Trait("Level", "L4")]
public sealed class SchemaRegistryAvroPlanTests
{
    [Fact]
    public async Task PreviewSchemaRegistryAvro_IncludesValueSchemas_AndDlq()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KsqlDsl:Common:BootstrapServers"] = "127.0.0.1:39092",
                ["KsqlDsl:Common:ClientId"] = "ut",
                ["KsqlDsl:SchemaRegistry:Url"] = "http://127.0.0.1:18081",
            })
            .Build();

        await using var ctx = new TestContext(config);
        var plans = ctx.PreviewSchemaRegistryAvro();

        Assert.Contains(plans, p => p.TopicName == "ut_orders" && p.ValueSubject == "ut_orders-value" && !string.IsNullOrWhiteSpace(p.ValueSchemaJson));
        Assert.Contains(plans, p => p.TopicName == "dead_letter_queue" && p.ValueSubject == "dead_letter_queue-value" && !string.IsNullOrWhiteSpace(p.ValueSchemaJson));
        Assert.Contains(plans, p => p.TopicName == "dead_letter_queue" && p.KeySubject == "dead_letter_queue-key" && p.KeySchemaJson is not null);
    }

    private sealed class TestContext : KafkaContext
    {
        public TestContext(IConfiguration configuration)
            : base(configuration, NullLoggerFactory.Instance) { }

        public EventSet<Order> Orders { get; set; } = null!;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>();
        }
    }

    [KafkaTopic("ut_orders")]
    private sealed class Order
    {
        public int Id { get; set; }
    }
}
