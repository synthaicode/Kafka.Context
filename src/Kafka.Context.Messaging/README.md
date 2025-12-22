# Kafka.Context.Messaging

Messaging primitives used by `Kafka.Context` (headers, meta, DLQ envelope).

Most users should install `Kafka.Context` and let it pull this package transitively.

## What is inside
- `MessageMeta`: identifies a consumed record (`Topic`, `Partition`, `Offset`, `TimestampUtc`).
- `DlqEnvelope`: the message shape written to/read from DLQ.

## Where you see these types
### Consume handler with headers/meta
```csharp
await context.Orders.ForEachAsync((order, headers, meta) =>
{
    Console.WriteLine($"{meta.Topic} {meta.Partition}:{meta.Offset}");
    return Task.CompletedTask;
});
```

### DLQ consume
```csharp
await context.Dlq.ForEachAsync((env, headers, meta) =>
{
    Console.WriteLine($"DLQ from {env.Topic} offset {env.Offset} ({env.ErrorType})");
    return Task.CompletedTask;
});
```

## Next steps
- Main package README: https://github.com/synthaicode/Kafka.Context
- Schema scaffold/verify CLI: https://www.nuget.org/packages/Kafka.Context.Cli

## AI Assist
If you're unsure how to use this package, run `kafka-context ai guide --copy` and paste the output into your AI assistant.
