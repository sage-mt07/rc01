# architecture_restart

## ❗️目的
- 複雑化したOSSをメンテナンス可能とすること、使いやすさを目的に内部処理を修正する
　具体的な問題：
        - Serialize/Deserializeの簡易化　： Confluent AvroSerializer/Deserializerの公式採用による再設計
        - KsqlContextBuilder／QueryBuilderの責任分割設計案    Contextは依存注入・全体統括、Queryは式解析・Key生成・Serialize呼び出し等、役割境界を明確化し肥大化を防止する設計方針とする。
        - pocoの属性廃止、FluentAPI化　（現在はpocoの属性と部分的なFluentAPIの運用となっているところをFluentAPIへ一本化）
        - RocksDBの採用：ToListAsyncではデフォルトをRocksDBの利用とする。

## ステップ
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
    - Coreからpoco用の属性を排除 
    - FluentAPIの追加　 

    ## 修正対象
     - readme.mdからリンクされてるファイル
     - src/Core/**
     - tests/**

    ## 📤 現状
    - Coreからpoco用の属性を排除　完了
    - FluentAPIの追加　 `core_namespace_redesign_plan.md` を作成済み
    1. FluentAPIの初期設計・実装サンプル作成（鳴瀬）
    -  FluentAPIによるPOCOモデル構成の「設計ガイドライン」「利用サンプルコード」を作成し、
    -　コア属性廃止後の推奨FluentAPI記述例
    - 既存POCO→FluentAPI移行フロー例をまとめてください。
    - 併せてMappingManagerとの連携例・ベストプラクティスもサンプルとして明記    
    ## 次の作業


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
    1. MappingManagerのAPI詳細仕様＆テスト観点リストのさらなる充実
    3. 残課題・次フェーズへのTODO洗い出し（PM天城主導）
    - 今回修正までの過程で未解決・追加検討が必要な論点をdiff_logなどに明文化
    - 次回マイルストーン（例：初期設計サイクルの確定／統合テスト設計着手など）を暫定設定し、チームへ共有

