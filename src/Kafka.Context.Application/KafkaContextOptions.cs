using Confluent.SchemaRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kafka.Context.Application;

public class KafkaContextOptions
{
    public ISchemaRegistryClient SchemaRegistryClient { get; set; } = null!;
    public ILoggerFactory? LoggerFactory { get; set; }
    public IConfiguration? Configuration { get; set; }

    public bool AutoRegisterSchemas { get; set; } = true;
    public bool EnableCachePreWarming { get; set; } = true;
    public bool FailOnInitializationErrors { get; set; } = true;
    public bool FailOnSchemaErrors { get; set; } = true;
    public TimeSpan SchemaRegistrationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (SchemaRegistryClient is null)
            throw new InvalidOperationException("SchemaRegistryClient is required");

        if (SchemaRegistrationTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("SchemaRegistrationTimeout must be positive");
    }
}

