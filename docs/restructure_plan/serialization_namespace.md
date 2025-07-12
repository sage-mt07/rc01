# Serialization Namespace 再設計方針

## 背景
`architecture_diff_20250711.md` で指摘された通り、現行実装では `AvroSerializer` / `AvroDeserializer` など独自実装が残存しています。再構築方針では Confluent 公式の AvroSerializer/Deserializer を採用し、独自管理層を段階的に廃止します。

## 主要変更点
1. **公式ライブラリへの統合**: Confluent.SchemaRegistry.Serdes を全面的に利用し、独自 `AvroSerializer` / `AvroDeserializer` はラッパーとして移行、または削除。
2. **SerializerFactory 再編**: `AvroSerializerFactory` を `ConfluentSerializerFactory` として再設計し、Schema Registry との連携を単純化。
3. **Abstractions の整理**: `IAvroSerializationManager<T>` など設定構造は最小限に残し、不要なキャッシュや管理クラスを削除。
4. **Schema 管理の明確化**: `Management` サブネームスペースは Schema Registry 連携に特化させ、バージョン管理を強化。

## 対応ファイル
- `src/Serialization/Avro/Core/AvroSerializer.cs` （削除または薄いラッパー化）
- `src/Serialization/Avro/Core/AvroDeserializer.cs` （同上）
- `src/Serialization/Avro/Core/AvroSerializerFactory.cs` （`ConfluentSerializerFactory` へ）
- `src/Serialization/Abstractions/AvroSerializationManager.cs` （整理）

## 備考
公式依存への移行に伴い、テストでは `Confluent.SchemaRegistry.Serdes` のモックを利用します。独自フォーマット互換の必要がなくなり、設定項目も整理可能となります。
