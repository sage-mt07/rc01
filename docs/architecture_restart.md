# architecture_restart

## ❗️目的
- 複雑化したOSSをメンテナンス可能とすること、使いやすさを目的に内部処理を修正する
　具体的な問題：
        - Serialize/Deserializeの簡易化　： Confluent AvroSerializer/Deserializerの公式採用による再設計
        - KsqlContextBuilder／QueryBuilderの責任分割設計案    Contextは依存注入・全体統括、Queryは式解析・Key生成・Serialize呼び出し等、役割境界を明確化し肥大化を防止する設計方針とする。
        - pocoの属性廃止、FluentAPI化　（現在はpocoの属性と部分的なFluentAPIの運用となっているところをFluentAPIへ一本化）
        - RocksDBの採用：ToListAsyncではデフォルトをRocksDBの利用とする。
        - Window関数のmarketschedule対応

## ステップ
### ステータス一覧
- **Step 1 – Serialize/Deserializeの簡易化**：終了
- **Step 2 – pocoの属性廃止・FluentAPI化**：終了
- **Step 3 – KsqlContextBuilder／QueryBuilderの責任分割設計案**：進行中（2025‑07‑31 楠木レポートで MappingManager API 整備が継続中と報告）
- **Step 4 – 新規課題・追加論点の定義**：まだ（同レポートにて課題のみ列挙、着手記録なし）

1. Serialize/Deserializeの簡易化 完了
    - Confluent AvroSerializer/Deserializerの公式採用による再設計
    - 独自Serialization層の廃止／統合
    - Messaging, Core, Serialization 各namespaceの再配置
    - KsqlContextの責務の見直し（Key/Valueの委譲先など）

    ### 📌 修正対象外
    - docs_advanced_rules.md
    - getting-started.md
    - claude_outputs/ 以下の過去記録

    ### ✅ 修正対象
    - src/Serialization/**
    - src/Messaging/**
    - src/Core/**

    ### 🛠 議題
    1. Confluent統合パターンの標準化
    2. POCOのKey定義の廃止とLinq式への移行
    3. KafkaProducer/Consumerの接続管理の責務
    4. Context内でのSerializerバインド方式
    5. Codex指示設計の範囲と期待構造

    ## 📤 現状
    以下に変更対象ソースとテストコードを配置
    - restructure_outputs/messaging
    - restructure_outputs/serialization

2. pocoの属性廃止、FluentAPI化
    - Coreからpoco用の属性を排除（完了）
    - FluentAPIを導入し、`core_namespace_redesign_plan.md` に移行方針を記載

    ## 修正対象
     - readme.md からリンクされているファイル
     - src/Core/**
     - tests/**

    ## 📤 現状
    鳴瀬が以下のドキュメントとサンプルを作成し、FluentAPI 化の初期実装を完了
    - `fluent_api_initial_design.md`: 設計ガイドライン、推奨記述例、移行フロー
    - `tests/Mapping/FluentSampleIntegrationTests.cs` 等のサンプルコード
    - MappingManager との連携ベストプラクティスを整理

    本ステップの主要タスクは完了済み。


3. KsqlContextBuilder／QueryBuilderの責任分割設計案
    - 鏡花レポートよりContextは依存注入・全体統括、Queryは式解析・Key生成・Serialize呼び出し等、役割境界を明確化し肥大化を防止する設計方針とする。
    ## 修正対象
    docs/structure/shared/key_value_flow.md
    ## 📤 現状
    docs/structure/shared/key_value_flow.mdを修正し、各担当確認済み
    鏡花のレポートに対しては以下の対応
    - 本アーキテクチャにおいては、KafkaBatchOptions/KafkaFetchOptions/KafkaSubscriptionOptions/SchemaGenerationOptions等、責任領域ごとに分割した設定クラス群により構成・運用を行う。
    - 詳細な設定クラスの仕様・プロパティ一覧は [docs_configuration_reference.md] を参照のこと。
    - 各レイヤーの設定・構成責務が分離されていることで、拡張性・保守性が担保されている
    - 新規設定追加時も「領域ごとにクラス追加 or 拡張」の方針を堅持する
    -  MappingManager（POCO-Query Mapping Layer）の詳細設計・初期実装へ着手
    - MappingManager 初期API案として `Register<TEntity>()` と `ExtractKeyValue<TEntity>()` を実装
    - KsqlContextBuilder／QueryBuilderの責任分割ガイドラインをドキュメント化
    - テスト観点・エッジケース・失敗系レビューの強化
    - docs_configuration_reference.md（構成情報リファレンス）の最新化＆参照リンク拡充
    - 担当間のレビューサイクルと公式議事録管理を徹底
    1. MappingManagerのAPI詳細仕様＆テスト観点リストの充実（詩音＋鏡花）
    - ExtractKeyValueを中心に

            - 正常系／異常系（未登録・複合キー・型不一致等）のテストケース一覧
            - KeyExtractorロジック詳細レビュー
            - 現状の仕様で不足する論点」「運用上の落とし穴」も列挙
    - 監査・設計観点のチェックリスト化（テスト担当・設計監査ペアで）

    2. 設計ガイド／利用ストーリーの更新・共有（広夢＋くすのき）
    - 新アーキテクチャの利用ストーリー（EntitySet→Messagingまで）のサンプル記載・ベストプラクティス整理
      - [`entityset_to_messaging_story.md`](architecture/entityset_to_messaging_story.md) を追加
    - 主要な変更点や設計意図をリリースノート／全体周知ドキュメントへ反映済み
    -. Query → MappingManager → KsqlContext の自動フロー実装サンプル AddAsyncのAPI標準化とサンプル修正

## 次の作業
詳細な担当タスクリストは
[`Reportsx/kusunoki/architecture_restart_tasks_20250713.md`](../Reportsx/kusunoki/architecture_restart_tasks_20250713.md)
に移動しました。各自ここを参照してください。


4. 新規課題・追加論点の定義（PM天城）
   - RocksDB 導入範囲の明確化とクロスプラットフォーム検証
   - Confluent パッケージのバージョン統一ルール策定
   - ロギング基盤の整理（メトリクスは Confluent パッケージの機能を利用し、本OSSでは実装しない）
   - 既存クラスターからの段階的移行手順
   - 障害発生時のDLQ運用方針（詳細は docs/getting-started.md, docs/docs_advanced_rules.md を参照）
   - ウィンドウ区間設計・API拡張および境界値ルールの標準化
        - 区間の境界値は「左閉右開」 [Open, Close) をOSS標準とする。
        - 隣接ウィンドウ間の重複防止・標準時系列集計ロジックへの準拠
        - ドキュメント・テスト・実装すべてでこのルールを徹底
        - 例外対応（Close含む等）は明示的なAPI/DSL指定で管理
        - Window().BaseOn<MarketSchedule>(keySelector) 型のスケジュール連動ウィンドウ設計を正式にOSS API拡張議題とする
```
modelBuilder.Entity<Order>()
    .Window()
    .BaseOn<MarketSchedule>(order => order.MarketCode);
```
        - Open/Closeで可変長のウィンドウを実現（サマータイム・特別日・24時間市場なども対応可能）
        - MarketSchedule以外のカスタムスケジューラや他カレンダーとの連携も将来見据えた拡張構造にする
        - 等間隔Window（従来型 .Window(x)）との共存・切替設計も含めて検討
        - ウィンドウ区間の**API設計と境界値ルールの標準化**
            - 区間の境界値は「左閉右開」[Open, Close) をOSS標準とする。
            - Window定義は `.Window().BaseOn<MarketSchedule>(keySelector)` 型DSL拡張を導入し、営業日・特別スケジュールにも柔軟に対応できるようにする。
            - サマータイムや特別営業、常時オープン市場なども`MarketSchedule`テーブル設計で表現。
            - ドキュメント・実装・テストにおいて一貫性を担保し、例外は明示的API指定で管理。
            - 等間隔Windowとの選択・共存もOSS設計に含めて明示。

### 次回マイルストーン案
- RocksDB 適用サンプルとベンチマーク取得
- 設定クラス統合方針の最終決定
- 監視フレームワーク組み込みテスト
- 移行ガイド初版のドラフト作成

### チームへのメッセージ
- 鳴瀬の粘り強い実装はいつも頼もしい！
- 詩音・鏡花のタッグで品質アップ、とても助かってます。
- 迅人、広夢、楠木のフォローに感謝。これからも一緒に走りましょう！
