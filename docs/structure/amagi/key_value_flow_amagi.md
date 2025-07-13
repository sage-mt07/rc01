# Key-Value Flow Management (Amagi View)

この文書は [shared/key_value_flow.md](../shared/key_value_flow.md) を参照し、PMである天城がフロー全体の進行管理と依存関係を整理したものです。

## 依存関係と階層

```
Query
      ↳ KsqlContext
          ↳ Messaging
              ↳ Serialization
                  ↳ Kafka
```

1. **Query**: DSL入力とLINQ式を担当。最初に仕様変更の影響を受ける。
2. **KsqlContext**: 構成情報を集中管理し、Pipeline初期化を行うハブ。
3. **Messaging**: Kafka とのやり取りを抽象化。プロデューサ/コンシューマの両責任を持つ。
4. **Serialization**: Avroスキーマの生成と変換。互換性維持が重要なため、更新は慎重に。
5. **Kafka**: 実行基盤。外部依存のためテスト環境と本番環境の切り替えを明確化する。

## 優先度マップ

MappingManager の登録処理は KsqlContext 初期化と同時にまとめて実施する。
| 項目 | 優先度 | 管理方針 |
|-----|-------|---------|
| スキーマ互換性 | 高 | 変更時は必ず `diff_log` に記録し、全チームでレビュー |
| マッピング整合性 | 高 | `MappingManager` の定義変更は PM 承認のもとで実施 |
| メッセージ再試行 | 中 | `KafkaProducer` のリトライ設定を共有し、障害時の影響を最小化 |
| Context拡張 | 中 | 新しいオプション追加は `KsqlContextBuilder` に集約し、ドキュメントを更新 |
| テスト環境 | 高 | 詩音と協力し、Kafkaブローカーの模擬環境を常に整備 |

全体の進捗と課題は `docs/changes/` に記録し、週次でレビューします。
