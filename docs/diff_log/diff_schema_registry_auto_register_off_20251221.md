# diff: Enforce Schema Registry auto-register OFF

## Summary
- Added `SchemaRegistry.AutoRegisterSchemas` (default `false`) to configuration.
- Producers now fail fast if auto-register is enabled and always use `AutoRegisterSchemas=false`.
- Documented the policy in the streaming wiki.

## Motivation
- Enforce a strict “register before produce” workflow to catch schema issues early.

## Files
- Updated: `src/Kafka.Context.Application/Configuration/KsqlDslOptions.cs`
- Updated: `src/Kafka.Context.Application/PublicAPI.Shipped.txt`
- Updated: `src/Kafka.Context.Infrastructure/Runtime/KafkaProducerService.cs`
- Updated: `src/Kafka.Context.Infrastructure/Runtime/DlqProducerService.cs`
- Updated: `docs/wiki/streaming-api.md`
