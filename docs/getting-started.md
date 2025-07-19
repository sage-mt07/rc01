# OSS設計資料：統合ドキュメント

## Overview

本ドキュメントは、Kafka.Ksql.Linq OSSの設計思想、アーキテクチャ、構成ルール、拡張指針を一体的にまとめた設計仕様書です。高度な利用者やOSS開発チーム向けに設計されており、全体像の把握と構成要素の関係理解を支援します。

## 目次 (Table of Contents)

-
  1. 設計原則
-
  2. アーキテクチャ概観
-
  3. POCO属性ベースDSL設計ルール
-
  4. POCO設計
-
  5. プロデュース操作
-
  6. コンシューム操作、（リトライ、エラー、DLQ、commitの誤解）
-
  7. ウィンドウ・テーブル操作
-
  8. ロギングとクエリ可視化
-
  10. 代表的な利用パターン

## 1. 設計原則

### 1.1 型安全・Fail Fast

- LINQベースでKSQL構文を表現し、ビルド時に構文誤りを排除、
AVROフォーマットの採用
- Context生成時に検出
- モード切替による型安全性の確保

####  🔍 検証時の強制レベル一覧（Strict / Relaxed モード）
検証項目|Strict|Relaxed|備考
---|---|---|---
Topic属性なし|❌ エラー|⚠️ 警告|クラス名をトピック名に使用
Key属性なし|⚠️ 警告|⚠️ 警告|Streamとして動作
抽象クラス|❌ エラー|❌ エラー|基本要件のため両方エラー
char型プロパティ|⚠️ 警告|⚠️ 警告|KSQL互換性の警告
未サポート型|⚠️ 警告|⚠️ 警告型|変換の警告

### 1.2 宣言的構文による表現力

- POCO + 属性 + LINQ = KSQLクエリ構築
- Entity Framework的な直感性を保つ

### 1.3 OSSとしての拡張性

- Builder、Query、Messaging、Windowなど明確な層構造
- Fluent APIによる構文追加・拡張が容易

## 2. アーキテクチャ概観

本OSSの構造は、Entity Framework の設計哲学に基づいて構築されています。POCO（Plain Old CLR Objects）に属性を付与し、LINQ式を用いて処理ロジックを記述することで、Kafka および ksqlDB の構造を宣言的に表現します。

これにより、Entity Framework に慣れた開発者が直感的にKafkaベースのストリーミング処理を設計・運用できるようになっています。各DSL操作（AddAsync, ForEachAsync, Window, Aggregate など）はEFの文法と類似性を持たせることで、学習コストの削減と記述一貫性を実現しています。

POCO（Plain Old CLR Objects）とは、依存性やフレームワーク固有の継承を持たない純粋なC#クラスを指します。本OSSでは、Kafka/KSQLの設定をこのPOCOに対する属性付与によって表現します。

このアプローチにより、構成情報とデータ定義が1つのクラスに集約され、Entity Frameworkと同様の直感的なコーディングスタイルを可能にしています。また、Fluent APIに頼らず、型安全かつ構文明快なDSLを構築することで、チーム内での可読性と再利用性も向上します。

kafkaへの接続エラーはksqlContextのコンストラクタでthrowされます。

> **POCO設計方針**
> POCO/DTO いずれでも `Key` 属性を使用せず、プロパティ定義順のみで key schema を決定します。
> 詳細は [docs/poco_design_policy.md](./poco_design_policy.md) を参照してください。

## 3. POCO属性ベースDSL設計ルール（Fluent APIの排除方針）

本OSSでは、Kafka/KSQLの設定をすべてPOCOクラスの属性で定義する方式を採用する。
これは、Fluent APIを用いたDSL記述の柔軟性と引き換えに、「構成がPOCOに集約されている」という明快さを重視した設計方針である。

### 3.1 型一覧

C#型
- bool
- int
- long
- float
- double
- string
- byte[]
- decimal
- DateTime
- DateTimeOffset
- Nullable型
- Guid
- short ,char ※keyに使用することはできません

### 3.2 プロパティ属性一覧

🧩 プロパティ属性一覧
|属性名	|説明|
|---|---|
[Key(Order = n)]|	KafkaのKeyに対応するプロパティ。複合キー可
[KafkaIgnore]	|スキーマ定義・KSQL変換から除外される
[DecimalPrecision(precision, scale)]	|decimal型の精度指定（例：18,4）
[DateTimeFormat("format")]	|KSQL上でのDateTimeの文字列フォーマット
[DefaultValue(value)]	|定義時のデフォルト値（スキーマ定義上のみ）
[MaxLength(n)]	|文字列長の制約。Avroスキーマにも反映

💡 サンプル：Orderエンティティの定義
```csharp
[Topic("orders", Partitions = 3, Replication = 1)]
[KsqlTable]
public class Order
{
    [Key(Order = 0)]
    public int OrderId { get; set; }

    [DateTimeFormat("yyyy-MM-dd")]
    public DateTime OrderDate { get; set; }

    [DecimalPrecision(18, 4)]
    public decimal TotalAmount { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [KafkaIgnore]
    public string? InternalUseOnly { get; set; }
}
```
### 3.3 クラス属性一覧

🏷️ クラス属性一覧
|属性名	|説明|
|---|---|
[Topic("topic-name")]	|Kafkaトピック名の指定（Partitions, Replicationも設定可能）
[KsqlStream] / [KsqlTable]	|Stream/Table の明示指定（未指定時は自動判定）

[Topic] 属性でトピックを定義。

パーティション数やレプリケーション係数のFluent APIによる設定をおこなう。
#### トピック定義
```csharp
// 属性によるマッピング
[Topic("orders", PartitionCount = 12, ReplicationFactor = 3)]
public class Order
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OrderTime { get; set; }
}

// 出力用DTO（PKを明示）
public class OrderDto
{
    [Key(Order = 0)]
    public string OrderId { get; set; }
    [Key(Order = 1)]
    public string CustomerId { get; set; }
    public decimal Amount { get; set; }
}

// Fluent API版
public class MyKsqlContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .WithPartitions(12)               
            .WithReplicationFactor(3);        
    }
}

```

🤖 自動判定ロジック
出力用 DTO/POCO の key schema はプロパティ定義順から自動生成されます。`KsqlTable` か `KsqlStream` かの判定は `KsqlTable`/`KsqlStream` 属性などのコンテキスト設定により決定されます。

Fluent APIでも指定可能です。

トピックのpartition, replication設定、Table/Streamの指定
```csharp
public class MyKsqlContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .AsStream();    //Tableの場合AsTable()                 
    }
}
```   
ただし、以下のメソッド呼び出しは設計原則違反となる。

🚫 制限事項
メソッド|	理由
|---|---|
.AsStream() / .AsTable()	|属性またはModelBuilderと重複可能。両方指定で一致しない場合はエラー

これらのメソッドは呼び出された場合に NotSupportedException をスローする設計とし、誤用を防止する。

### Push/Pull Query の明示
`DefineQuery` で使用するクエリビルダーは `.AsPush()` / `.AsPull()` により実行モードを指定できます。明示しない場合は `Unspecified` 扱いとなり、Schema Registry 登録時に Pull クエリの制約違反が検出されると自動で Push (`EMIT CHANGES` 付き) へ切り替わります。
※その他の詳細設定はdev_guide.md参照

## 4. スキーマ構築と初期化手順（OnModelCreating）

Kafka.Ksql.Linq では、POCOクラスに定義された属性情報をもとに、OnModelCreating メソッドを通じて Stream/Table としてのスキーマ登録を行います。

この初期化処理により、POCOの構造は Kafka/KSQL に対する明確なスキーマとして解釈され、後続の LINQ クエリが正しく処理される基盤となります。

✅ 実装のポイント

KsqlContext を継承したクラス内で、modelBuilder.Entity<T>() を用いて POCO を登録します。

`KsqlStream` または `KsqlTable` 属性が無い場合でも、プロパティ定義順から生成される key schema を基に自動的に Table/Stream が推定されます。

この登録時点で DSL の構文検証が行われ、構文誤りや属性不備はここで Fail Fast となります。

Schema Registry への接続もこの時点で必要となり、未接続・未整備の場合には例外が発生します。

```csharp

[KsqlStream]
[Topic("orders")]
public class Order
{
    public string OrderId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    [DecimalPrecision(18, 2)]
    public decimal Amount { get; set; }
}

[KsqlTable]
[Topic("customers")]
public class Customer
{
    public string CustomerId { get; set; }
    public string Name { get; set; }
}

// 出力用DTO（定義順で自動的にキー生成）
public class CustomerDto
{
    public string CustomerId { get; set; }
    public string Name { get; set; }
}

public class MyKsqlContext : KsqlContext
{
    protected override void OnModelCreating(KsqlModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        modelBuilder.Entity<Customer>()
            .Where(c => c.Name != null)
            .Select(c => new { c.CustomerId, c.Name });
        
    }
}
```   

このように、POCOの登録はアプリケーションの起動時に実施されることで、DSL全体の整合性とスキーマ妥当性を確保します。

### ダミーデータ投入によるスキーマ確定

CREATE TABLE/STREAM を実行してテーブルを登録した直後は、KSQL 側がスキーマ情報を完全に認識するまで時間がかかる場合があります。スキーマ未確定の状態で `SELECT` などの DML を実行すると `column 'REGION' cannot be resolved` といったエラーが発生するため、各テーブルに対応する Kafka トピック（例: `orders`, `customers`）へ **1 件以上のダミーレコード** を **AVRO** 形式で送信してください。全てのカラムを埋めたレコードを投入した後に DML クエリを実行することで、カラムスキーマが正しく取得されます。テストコードではこのダミーデータ送信をセットアップ処理に組み込むことを推奨します。

テスト目的で送信するダミーメッセージには `is_dummy=true` といったヘッダーを付与することで、consumer や KSQL 側で本番データと区別できます。このヘッダー値を利用して、スキーマ確定後のクリーンアップや検証を行ってください。
詳細なテスト手順は `features/dummy_flag_test/instruction.md` も併せて参照してください。

スキーマ登録済みのエンティティ名やフィールド名は **大文字・小文字を区別** します。`SchemaRegistryException` が発生する場合は、Schema Registry に登録されているスキーマと、テストコードで利用しているモデルの名前が完全に一致しているか確認してください。

## 5. プロデュース操作
Kafka.Ksql.Linq では、AddAsync() メソッドを使用して、POCOベースのデータをKafkaトピックへ送信（プロデュース）することができます。LINQ構文と統合されており、型安全かつ直感的なメッセージ生成を実現します。

### ✅ 実装のポイント
AddAsync() により、指定トピック（POCOに定義された [Topic]）へメッセージを送信

送信処理は非同期（Taskベース）であり、awaitable

Fail Fastにより、スキーマ不整合や未設定項目は実行前に検出されます

送信前にログでメッセージ内容を確認可能（詳細は第8章のロギング参照）

🧪 サンプルコード
```csharp
var order = new Order
{
    OrderId = "ORD-001",
    Timestamp = DateTimeOffset.UtcNow,
    Amount = 1234.56m
};

await context.AddAsync(order);
```  
このコードでは、Orderエンティティに基づいてKafkaに1件のメッセージが送信されます。トピックやスキーマ定義はPOCO属性に基づいて自動的に解決されます。


### ✅ DLQ 送信を行う場合の方法
AddAsync() を使ったDLQ活用例（利用側）

```csharp
try
{
    await context.Orders.AddAsync(order);
}
catch (Exception ex)
{
    await context.SendToDlqAsync(order, ex, "AddAsyncFailure");
}
```

この方式により、AddAsync() の利用者はエラーハンドリングを自分で制御しつつ、DLQ機能も安全に併用することが可能です。

ChatGPT:
了解しました。次の章は以下の 6. コンシューム操作、（リトライ、エラー、DLQ、commitの誤解） です。以下のように表示を開始します：

## 6. コンシューム操作、（リトライ、エラー、DLQ、commitの誤解）
この章では、Kafkaメッセージの コンシューム処理 に関する基本操作と、以下のような誤解されやすいポイントを整理します：

### ✅ 基本的な消費処理の構文
```csharp
await context.Orders
    .Where(o => o.Amount > 1000)
    .Select(o => Process(o))
    .ForEachAsync();
```

### 🧠 よくある誤解と実態
誤解されやすい概念	|実際の挙動	|補足説明
|---|---|---|
ForEachAsync にリトライ機能がある|	ない	|エラー処理は .OnError() により明示的に定義が必要
DLQは自動的に処理される|	明示 or .OnError(ErrorAction.DLQ) 必須	|設定がないとDLQ送信されない
処理失敗時もcommitされる|	commitは常に明示または .WithAutoCommit() 指定|	自動ではないため注意
メッセージスキップはデフォルトで行われる|	明示的に .OnError(ErrorAction.Skip) 指定が必要	|スキップ設定なしでは処理停止の可能性

🛠 使用例（リトライ＋DLQ）
```csharp
await context.Orders
    .OnError(ErrorAction.DLQ)
    .WithRetry(3)
    .ForEachAsync(order => Handle(order));
```

このように、明示的なエラーハンドリング設計が求められます。

### commitの制御
Kafkaのコンシューム操作において、メッセージのオフセットコミットは非常に重要です。

デフォルトでは 自動 commit（Auto Commit） が有効となっており、
明示的な指定がない場合でも、処理が成功した時点で commit が行われます。

ただし、エラーハンドリングや再処理設計の都合上、明示的に commit 制御をしたい場合は、
WithManualCommit() による設定が必要です。

自動 commit を前提とする場合でも、明示的に .WithAutoCommit() を記述することで、
意図を明確にすることができます：

```csharp
public class MyKsqlContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .WithManualCommit();  // ✅ 手動コミット設定
    }
    // 手動コミット例
    public async Task ManualCommitExample()
    {
      var orders = context.Set<Order>();

      await orders.ForEachAsync(async orderMessage => {
        // WithManualCommit()設定時はIManualCommitMessage<T>が返される
        if (orderMessage is IManualCommitMessage<Order> manualCommitMessage)
        {
            try
            {
                // メッセージ処理
                var order = manualCommitMessage.Value;
                await ProcessOrder(order);
                
                // ✅ 処理成功時にコミット
                await manualCommitMessage.CommitAsync();
                
                Console.WriteLine($"Successfully processed and committed order: {order.Id}");
            }
            catch (Exception ex)
            {
                // ✅ 処理失敗時にネガティブACK
                await manualCommitMessage.NegativeAckAsync();
                
                Console.WriteLine($"Failed to process order: {ex.Message}");
                throw;
            }
        }
    });
  }
  // retry例
  public async Task RetryWithManualCommitExample()
  {
    var orders = context.Set<Order>()
        .OnError(ErrorAction.Retry)  // ✅ リトライ設定
        .WithRetry(maxRetries: 3, retryInterval: TimeSpan.FromSeconds(2));

    await orders.ForEachAsync(async orderMessage => {
        if (orderMessage is IManualCommitMessage<Order> manualCommitMessage)
        {
            try
            {
                var order = manualCommitMessage.Value;
                await ProcessOrder(order);
                
                // ✅ 処理成功時にコミット
                await manualCommitMessage.CommitAsync();
                
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] SUCCESS: Order {order.Id} processed and committed");
            }
            catch (Exception ex)
            {
                // ✅ EventSetのRetry機能が働く（内部的にリトライ実行）
                // 最終的にリトライ失敗した場合のみここに到達
                await manualCommitMessage.NegativeAckAsync();
                
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] FINAL_FAILURE: Order processing failed after retries: {ex.Message}");
                throw;
            }
        }
    });
  }
}
```

## 7. ウィンドウ・テーブル操作

⏳ Window 機能の設計

### 概要

KSQLにおけるWindow処理は、時間単位での集計や状態管理を行う際に使用されます。本OSSでは、LINQ構文からWindow処理に対応するDSLを提供し、Kafka Streamsによるウィンドウ処理のKSQL変換を自動化しています。

### 対象エンティティ

ウィンドウ処理は Set<T> に対して .Window(x) を指定することで適用され、内部的に WindowConfiguration として扱われます。
```
modelBuilder.Entity<Order>()
    .Window(new[] { 1, 5, 60 });
```

この設定により、1分足、5分足、60分足の3種類のウィンドウが定義され、各ウィンドウに対応する状態管理とKSQLクエリが生成されます。

🚩最重要パターン宣言

本OSSでは「1つのPOCO＋Window属性（例：WindowMinutes）」で1分足・5分足・60分足など“複数ウィンドウ集約”を一元管理する設計が王道です。

サンプルやサードパーティAIもこの設計をまず推奨・実装しています。

### Window Finalization

ウィンドウ処理で生成されたデータは、一定時間経過後に「確定」され、*_window_{minutes}_final 形式のトピックに書き出されます。確定処理は WindowFinalizationManager により管理され、以下の責務を持ちます：

- 複数のPODから送信された同一Windowキーのデータをマージ
- 指定分単位でタイマーを駆動し、該当Windowを確定
- KafkaトピックへFinalメッセージを書き込み

このとき、元のWindowデータとは異なるトピックに送信されるため、事前に _window_final トピックの作成が必要です。また、元のトピックに新しいデータが送られなくても、タイマーによりx分単位でFinalデータが自動生成されます。

初期化時、すべての _window_final トピックは EnsureWindowFinalTopicsExistAsync により事前に作成されます。この処理は OnModelCreating 後のステージで自動的に実行され、各エンティティの .Window(...) 設定に基づいて必要なFinalトピックを準備します。

### AvroTimestamp の利用

Window処理で使用される時間情報は、すべて AvroTimestamp 型で管理されます。これにより：

- Avroシリアライズ時のUTC変換とスキーマ整合性を確保
- WindowStart/End の精度と互換性を保証
- フィールドには [AvroTimestamp] 属性を付与
```

public class WindowedOrderSummary
{
    [AvroTimestamp]
    public DateTime WindowStart { get; set; }

    [AvroTimestamp]
    public DateTime WindowEnd { get; set; }

    public int Count { get; set; }
}
```

### 課題と補足

- .Window(...) で複数の粒度（例: 1, 5, 60分）を定義した場合、それぞれに対応する _window_{minutes}_final トピックが必要です。
- Kafka設定で auto.create.topics.enable = false が指定されている場合、本OSSでは初期化処理中に EnsureWindowFinalTopicsExistAsync を用いてすべての Final トピックを自動作成します。
- Final トピックのスキーマは WindowFinalMessage に準拠して自動登録されます。
- 元のデータが送信されなくても、指定時間が経過すれば Final データは内部タイマーにより自動的に生成されます。

このWindow機能は、リアルタイムな時間軸集計や、複数粒度でのKTable生成に対応するための中核機能となります。


## 8.ロギングとクエリ可視化

ロギングとクエリ可視化

本OSSでは、namespace単位でのログ出力制御を行い、必要な情報のみをDebugレベルで可視化する設計としています。appsettings.json の例：
```

"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Kafka.Ksql.Linq.Serialization": "Debug",
    "Kafka.Ksql.Linq.Messaging": "Warning",
    "Kafka.Ksql.Linq.Core": "Information"
  }
}
```
クエリのログ出力を詳細に行いたい場合は、以下の設定を追加することで KSQL 変換処理を対象とできます：
```
"Kafka.Ksql.Linq.Query": "Debug"
```
これにより、KSQLの変換処理ログを確認することが可能です。

## 9. 削除と件数制限の操作

### Set<T>().Limit(N)
`Limit` を付与すると、KSQL の `LIMIT` 句を伴う Pull Query が生成されます。取得数が N
 件に達した時点で処理が完了し、それ以降のレコードは自動的に破棄されます。

```csharp
var latest = await context.Set<Trade>()
    .Limit(50)
    .ToListAsync();
```

バー生成に `WithWindow().Select<TBar>()` を使用している場合、`BarTime` への
代入式から自動的にタイムスタンプセレクターが取得され、`Limit` の並び替えに活用
されます。

### RemoveAsync でトムストーン送信
`RemoveAsync` はキーを指定して値 `null` のメッセージ（トムストーン）をトピックへ送信し
ます。これにより KTable やキャッシュに保持された同一キーのデータが削除されます。

```csharp
await context.Trades.RemoveAsync(tradeId);
```

## 10. 代表的な利用パターン