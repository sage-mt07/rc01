# Manual Commit Example

This sample demonstrates manual acknowledgement using **Kafka.Ksql.Linq**.
`Program.cs` defines a `ManualCommitOrder` entity configured with `.WithManualCommit()`.
During consumption each record is received as `IManualCommitMessage<T>`.
Successful processing calls `.CommitAsync()`; failures call `.NegativeAckAsync()` to trigger retry.

## Prerequisites

- .NET 8 SDK
- Docker (for Kafka and ksqlDB)

## Setup

1. Start the local Kafka stack:
   ```bash
   docker-compose up -d
   ```
2. Run the example:
   ```bash
   dotnet run --project .
   ```

## Design Document References

- [手動コミット操作](../../docs/manual_commit.md)
- [POCO属性設計](../../docs/oss_design_combined.md#3-poco属性ベースdsl設計ルール（fluent-apiの排除方針）)
- [スキーマ初期化](../../docs/oss_design_combined.md#4-スキーマ構築と初期化手順（onmodelcreating）)
\nSee the manual commit API in [api_reference.md](../../docs/api_reference.md).
