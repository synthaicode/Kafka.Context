# Kafka.Context.Abstractions

Shared abstractions and attributes used by `Kafka.Context`.

Most users should install `Kafka.Context` and let it pull this package transitively.

## What is inside
- Attributes used to describe Kafka topic and Avro mapping contracts:
  - `KafkaTopicAttribute`
  - `KafkaKeyAttribute`
  - `KafkaTimestampAttribute`
  - `KafkaDecimalAttribute`
  - `SchemaSubjectAttribute` (Schema Registry subject metadata; used by CLI verify)
  - `SchemaFingerprintAttribute` (schema fingerprint metadata; used by CLI verify)
- Modeling abstractions used by `KafkaContext.OnModelCreating(...)` (e.g., `IModelBuilder`).

## Minimal example (attributes)
```csharp
using Kafka.Context.Attributes;

[KafkaTopic("orders")]
public sealed class Order
{
    [KafkaKey] public int OrderId { get; set; }
    [KafkaDecimal(9, 2)] public decimal Amount { get; set; }
}
```

## Schema Scaffold / Verify (CLI, optional)

If you want SR (Schema Registry) fingerprint checks in CI/dev, use the dotnet tool:

```sh
dotnet tool install -g Kafka.Context.Cli
```

See https://www.nuget.org/packages/Kafka.Context.Cli.

## Next steps
- Main package README: https://github.com/synthaicode/Kafka.Context
- Configuration reference (`appsettings.json`): https://github.com/synthaicode/Kafka.Context/blob/main/docs/contracts/appsettings.en.md
