## 🏗️ Architecture Overview（全体構造と各層の責務）

本ドキュメントは、Kafka.Ksql.Linq OSS の内部構造と各レイヤーの責務を明確にすることで、拡張・保守・デバッグ時の理解を支援することを目的としています。

⚠️ 本資料は DSL を使うだけのユーザー向けではなく、OSS本体の改変・拡張に関わる開発者向けです。

---

### 🧱 レイヤー構造と責務

| レイヤー名                       | 主な責務概要                                                                 |
|----------------------------------|------------------------------------------------------------------------------|
| Application層                   | DSL記述（`KsqlContext`継承 + `OnModelCreating`）                             |
| Context定義層                   | DSL解析とモデル構築（`KsqlContext`, `KsqlModelBuilder`）                    |
| Entity Metadata管理層           | POCO属性解析、Kafka/Schema Registry 用設定生成                              |
| クエリ構築層（LINQ→KSQL変換）   | LINQ式解析、KSQL構文生成、式ツリー訪問                                     |
| ストリーム構成層               | KStream/KTable構成、Window処理、Join、DLQ、Final出力など                    |
| Kafka I/O層（外部連携）         | Kafkaクラスタ接続、トピック管理、RocksDB操作、Schema Registry連携など     |

各レイヤーの詳細構造や主なクラスについては、`docs/namespaces/` 配下にて Namespace 単位で説明されます。

---

### 🔁 他ドキュメントとの関係

- `docs_configuration_reference.md` → DSLとappsettingsのマッピング解説
- `docs_advanced_rules.md` → 運用時の制約と設計判断の背景
- `dev_guide.md` → 機能追加・DSL拡張手順の実装ルール
- `docs/namespaces/*.md` → 各層に対応するNamespaceごとの実装責務と拡張ポイント

---

本ドキュメントは、設計構造の俯瞰と責務分離の理解を促すものであり、拡張時の出発点・索引として活用されます。

※ 図解や依存関係マップは別紙予定

