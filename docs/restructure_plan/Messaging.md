# Messaging Namespace Restructure Plan

## Summary
KafkaProducer/Consumer pooling will be removed. The namespace will rely on Confluent.Kafka types and expose only thin wrappers for DSL integration.

## Motivation
`architecture_diff_20250711.md` highlighted leftover pool classes and unclear Confluent integration. To streamline the architecture we adopt Confluent's official serializer and unify connection management.

## Scope
Messaging namespace files related to producer/consumer management and exceptions. Configuration handling will move to the Application layer.

## Affected Files and Approach
- `src/Messaging/Producers/Core/PooledProducer.cs` – delete
- `src/Messaging/Consumers/Core/PooledConsumer.cs` – delete
- `src/Messaging/Consumers/Core/ConsumerInstance.cs` – refactor for direct client use
- `src/Messaging/Producers/KafkaProducerManager.cs` – simplify connection logic
- `src/Messaging/Consumers/KafkaConsumerManager.cs` – same as above
- `src/Messaging/Exceptions/ProducerPoolException.cs` – delete

## Notes and Concerns
Removal of pooling changes `KsqlContext` behaviour. Tests for connection caching and consumer life cycle must be updated.
