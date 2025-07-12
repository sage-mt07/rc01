# Serialization Namespace Restructure Plan

## Summary
Confluent.SchemaRegistry.Serdes will become the primary serialization component. Existing custom AvroSerializer and AvroDeserializer classes are removed or reduced to thin wrappers.

## Motivation
`architecture_diff_20250711.md` identified that our independent serializer layer duplicates Confluent functionality. Migration to the official components simplifies schema registration and reduces maintenance.

## Scope
Serialization namespace, focusing on Avro support and factory classes. Management and abstraction layers will be trimmed to the minimal interfaces needed by application code.

## Affected Files and Approach
- `src/Serialization/Avro/Core/AvroSerializer.cs` – delete or replace with a wrapper using Confluent serializers
- `src/Serialization/Avro/Core/AvroDeserializer.cs` – same as above
- `src/Serialization/Avro/Core/AvroSerializerFactory.cs` – rename and refactor to `ConfluentSerializerFactory`
- `src/Serialization/Abstractions/AvroSerializationManager.cs` – simplify and remove caching logic

## Notes and Concerns
Tests should mock `Confluent.SchemaRegistry.Serdes` to avoid external dependencies. Version management for schemas will move into the Management subnamespace and rely on Schema Registry features.
