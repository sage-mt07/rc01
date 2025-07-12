# KsqlContext Restructure Plan

## Summary
KsqlContext will delegate connection and serializer configuration to KsqlContextBuilder and application configuration. The context itself only coordinates query execution and EntitySet creation.

## Motivation
`architecture_diff_20250711.md` highlighted excessive responsibilities within KsqlContext, including managing Kafka clients and serializers. Aligning with the restart design splits these concerns and improves testability.

## Scope
The refactor touches the KsqlContext class, its builder and options objects, and any serialization dependencies. Only runtime orchestration remains in the context.

## Affected Files and Approach
- `src/KsqlContext.cs` – remove connection management and restrict to query execution
- `src/Application/KsqlContextBuilder.cs` – create Kafka clients and inject serializers
- `src/Application/KsqlContextOptions.cs` – define immutable configuration consumed by the builder
- `src/Serialization/` – update factory references and DI hooks

## Notes and Concerns
Unit tests must adapt to builder-based creation. DI containers should supply mocked producer and consumer instances to isolate context behaviour.
