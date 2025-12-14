# Kafka.Context

Kafka + Schema Registry を「Context中心・契約駆動」で扱うための、軽量で意見のある I/O ランタイム（契約形式: Avro）。

NuGet: `Kafka.Context`

Target frameworks: .NET 8 / .NET 10

## What *This* Project Is *Not*

This is **not**:
- a Kafka Streams / ksqlDB engine
- a query generator (no JOIN/Window/Aggregation)
- a middleware framework (no bus/worker stack)
- a KafkaFlow dependent library
- an ORM for Kafka

This *is*:
- a context-centric Kafka + Schema Registry IO layer
- contract-first producer/consumer abstraction
- lightweight, predictable, and opinionated

## MVP scope
- Provisioning: topic create/verify + Schema Registry register/verify (fail fast)
- IO: `AddAsync(key, poco)` / `ForEachAsync(handler)` / DLQ + retry

See `overview.md` for the repository map and scope.
Target code shape sample: `docs/samples/target_code_shape.md`.
Contracts: `docs/contracts/`.

## Kafka.Context Kansei Model

 Context = (Cluster + Schema Registry)

 Context.Provision(...)   Topic & SR registration
 Context.AddAsync(key, valuePOCO)
 Context.ForEachAsync(handler)
 Context.Dlq.ForEachAsync(handler)
