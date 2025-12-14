# Kafka.Context

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


## Kafka.Context Kansei Model

 Context = (Cluster + Schema Registry)

 Context.Provision(...)   ← Topic & SR registration
 Context.AddAsync(key, valuePOCO)
 Context.ForEachAsync(handler)
 Context.Dlq.ForEachAsync(handler)
