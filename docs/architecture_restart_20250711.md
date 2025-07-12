# architecture_restart_20250711.md

## ❗️会議の目的
- Confluent AvroSerializer/Deserializerの公式採用による再設計
- 独自Serialization層の廃止／統合
- Messaging, Core, Serialization 各namespaceの再配置
- KsqlContextの責務の見直し（Key/Valueの委譲先など）

## 📌 修正対象外
- docs_advanced_rules.md
- getting-started.md
- claude_outputs/ 以下の過去記録

## ✅ 修正対象
- src/Serialization/**
- src/Messaging/**
- src/Core/**

## 🛠 議題
1. Confluent統合パターンの標準化
2. POCOのKey定義の廃止とLinq式への移行
3. KafkaProducer/Consumerの接続管理の責務
4. Context内でのSerializerバインド方式
5. Codex指示設計の範囲と期待構造

## 📤 次ステップ
- 鳴瀬へKafkaProducerBuilderのCodex指示投入
- 鏡花による再構成レビュー（docsに記録）
- Codexによる再出力の妥当性検証
