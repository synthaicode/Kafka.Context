# diff_runtime_safety_20260101

## Summary
- Make manual-commit tracking thread-safe in EventSet/DynamicTopicSet.
- Ensure consumer Close() runs on exceptions.
- Propagate cancellation to DLQ production where available.
- Avoid sync-over-async in dynamic windowed key decoding.
- Add concurrency safety for streaming registries and improve admin cancellation handling.

## Files
- src/Kafka.Context/EventSet.cs
- src/Kafka.Context/DynamicTopicSet.cs
- src/Kafka.Context.Infrastructure/Runtime/KafkaConsumerService.cs
- src/Kafka.Context.Infrastructure/Runtime/KafkaDynamicConsumerService.cs
- src/Kafka.Context.Infrastructure/Admin/KafkaAdminService.cs
- src/Kafka.Context.Streaming/Streaming/StreamingQueryRegistry.cs
- src/Kafka.Context.Streaming/Streaming/StreamingSourceRegistry.cs
