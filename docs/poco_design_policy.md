# POCO設計・PK運用・シリアライズ方針

本ドキュメントでは OSS における POCO 設計方針、PK 運用および serialize/deserialize の基本ポリシーを整理する。内容は [reports/20250708.txt](../reports/20250708.txt) をもとに、鏡花（品質管理）・楠木（記録管理）・広夢（広報）の協働で取りまとめた。

## 1. POCO 設計原則
- 業務 POCO は **純粋な業務データ構造** とし、キー指定用の属性は付与しない。
- DB 都合やビジネスロジック都合で自由に設計し、Kafka の key schema を意識しない。

## 2. PK 運用ルール
- PK (key schema) は DTO/POCO の **プロパティ定義順** のみを基準に自動生成する。
- `Key` 属性は設計・実装から除外し、複合キーの順序は DTO/POCO の定義順に従う。
- キーに利用できる型は `int` `long` `string` `Guid` の4種類のみとする。その他の型は
  サポート外であり、利用者側で変換を行うこと。
- LINQ `group by` 等で指定した論理 PK の順序と一致させる。
- `GroupBy` や `Join` で生成されるキー順と DTO/POCO の定義順が一致しない場合、初期化時に
  `InvalidOperationException` を送出する。エラーメッセージは
  **"GroupByキーの順序と出力DTOの定義順が一致していません。必ず同じ順序にしてください。"** とする。
  これにより CLI/ビルド時・ユニットテスト実行時の双方で不整合を早期検出できる。

## 3. シリアライズ／デシリアライズ方針
- OSS は POCO ⇔ key/value 構造体の変換を **完全自動** で実行する。
- Produce 時は DTO/POCO から PK 部と Value 部を自動分離してシリアライズする。
- Consume 時は Kafka から受信した key/value をデシリアライズし、 DTO/POCO に再構成する。
- シリアライザ／デシリアライザは型・スキーマ毎にキャッシュし性能を確保する。

## 4. 運用上のポイント
- 以上の方針は全ドキュメント・ガイドに明記し、チーム全員で遵守する。
- 進捗や課題があれば天城（PM）へ随時エスカレーションする。

---
作成: 広夢 / 監修: 鏡花・楠木

### 関連ドキュメント
- [getting-started.md](./getting-started.md)
- [docs_advanced_rules.md](./docs_advanced_rules.md)
