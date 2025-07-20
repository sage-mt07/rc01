# API Reference (Draft)

この文書は `Kafka.Ksql.Linq` OSS における公開 DSL/API と主要コンポーネントの概要を整理したものです。今後の設計ドキュメントや実装コード、テストコードへの参照基盤として利用します。

## 既定値の参照

- 既定値一覧は [docs_configuration_reference.md](docs_configuration_reference.md) を参照してください。

### テスト設計上の注意
- Kafka メッセージ送信は `Chr.Avro.Confluent` を利用した POCO 型の自動スキーマ連携を推奨します。
- `GROUP BY` を含む Pull Query はサポートせず、Push Query (`EMIT CHANGES`) のみ利用可能です。
- `WINDOW` 句は `GROUP BY` の直後に配置してください。
- `CASE` 式では `THEN`/`ELSE` の型を必ず一致させる必要があります。
- `MIN`/`MAX` 集計はテーブルに対して行わず、STREAM クエリで検証します。

## Context クラスとベースインタフェース

| API                   | 説明                                   | 対象レイヤ | 実装状態 |
|------------------------|----------------------------------------|------------|---------|
| `IKsqlContext`         | コンテキスト操作の抽象インタフェース   | Context    | ✅      |
| `KafkaContextCore`     | `IKsqlContext` 実装の基底クラス        | Context    | ✅      |
| `KsqlContext`          | Kafka連携を統合した抽象コンテキスト    | Context    | ✅      |
| `KsqlContextBuilder`   | `KsqlContextOptions` 構築用ビルダー   | Application| ✅      |
| `KsqlContextOptions`   | スキーマレジストリ等の設定保持        | Application| ✅      |
| `IEventSet<T>`         | LINQ/Streaming操作の共通インタフェース| Stream/Table| ✅     |
| `IManualCommitMessage<T>` | 手動コミットメッセージ             | Subscription| ✅     |

## LINQ 風 DSL 一覧

| DSL メソッド                   | 説明                          | 戻り値型                          | 対象レイヤ    | 実装状態 |
|--------------------------------|-------------------------------|-----------------------------------|---------------|---------|
| `.Where(predicate)`            | 条件フィルタ                  | `IEventSet<T>`                    | Stream/Table  | ✅      |
| `.Window(WindowDef \| TimeSpan)` | タイムウィンドウ指定       | `IQueryable<T>`                   | Stream        | ✅      |
| `.Window(int minutes)`          | `WindowMinutes`によるフィルタ  | `IEntitySet<T>`                  | Stream/Table  | ✅      |
| `.Window().BaseOn<TSchedule>(keySelector, ?openProp, ?closeProp)` | `[ScheduleRange]` 属性、または `openProp`/`closeProp` パラメータで開始/終了を示すスケジュールPOCOに基づきウィンドウを生成 | `IQueryable<T>` | Stream | ✅ |
| `.GroupBy(...)`                | グループ化および集約          | `IEventSet<IGrouping<TKey, T>>`   | Stream/Table  | ✅      |
| `.OnError(ErrorAction)`        | エラー処理方針指定            | `EventSet<T>`                     | Stream        | ✅      |
| `.WithRetry(int)`              | リトライ設定                  | `EventSet<T>`                     | Stream        | ✅      |
| `.StartErrorHandling()`        | エラーチェーン開始            | `IErrorHandlingChain<T>`          | Stream        | ✅      |
| `.WithManualCommit()`          | 手動コミットモード切替        | `IEntityBuilder<T>`               | Subscription  | ✅      |
| `.Limit(int)`                  | 取得件数を制限する Pull Query | `IEntitySet<T>`                  | Stream/Table  | ✅      |

- `ToList`/`ToListAsync` は Pull Query として実行されます【F:src/Query/Pipeline/DMLQueryGenerator.cs†L27-L34】。
- `WithManualCommit()` を指定しない `ForEachAsync()` は自動コミット動作となります【F:docs/old/manual_commit.md†L1-L23】。
- `OnError(ErrorAction.DLQ)` を指定すると DLQ トピックへ送信されます【F:docs/old/defaults.md†L52-L52】。
- Messaging クラス自体は DLQ 送信処理を持たず、`ErrorOccurred`/`DeserializationError`/`ProduceError` などのイベントを通じて外部で DLQ 送信を行います。
- `Set<T>().Limit(n)` を指定すると n 件取得後にストリームが終了します。残りのレコードは破棄されます。
- バーエンティティでは `WithWindow().Select<TBar>()` で `BarTime` に代入した式が自動的に記録され、`Limit` の並び替えに使用されます。
- `RemoveAsync(key)` は値 `null` のトムストーンを送り、KTable やキャッシュから該当キーのデータを削除します。
- `.Window().BaseOn<TSchedule>` を用いる場合、バーは `[ScheduleRange]` 属性、または `openProp`/`closeProp` パラメータで示された `Open` ～ `Close` の範囲に含まれるデータのみで構成されます。日足生成で `Close` が 6:30 のときは、6:30 未満のデータが当日の終値として扱われます。
- バーやウィンドウ定義は必ず `KafkaKsqlContext.OnModelCreating` 内で宣言してください。アプリケーション側では定義済みの `Set<T>` を参照するだけです。
- `WithWindow<Rate, MarketSchedule>()` に続けて `.Select<RateCandle>()` を呼び出すことで、レートからバーエンティティを構成できます。
- `WithWindow<TEntity, TSchedule>(windows, timeSelector, rateKey, scheduleKey)` として `timeSelector` 引数でウィンドウを区切る時刻プロパティを明示します。

これらの戻り値型を把握することで、DSLチェーンにおける次の操作を判断しやすくなります。特に `OnError()` や `WithRetry()` は `EventSet<T>` を返すため、続けて `IEventSet` 系メソッドを利用できます。

## 属性 (Attribute) 定義

| 属性                       | 役割                           | 実装状態 |
|----------------------------|--------------------------------|---------|
| `DefaultValueAttribute`    | 既定値指定                     | ✅      |
| `MaxLengthAttribute`       | 文字列長制限                   | ✅      |
| `ScheduleRangeAttribute`   | 取引開始・終了をまとめて指定する属性 | 🚧 |

`WithDeadLetterQueue()` は過去の設計で提案されましたが、現在は `OnError(ErrorAction.DLQ)` に置き換えられています。

## 構成オプションとビルダー

| API                        | 説明                             | 実装状態 |
|----------------------------|----------------------------------|---------|
| `KsqlDslOptions`           | DLQ 設定や ValidationMode など DSL 全体の構成を保持 | ✅ |
| `ModelBuilder`             | POCO から `EntityModel` を構築するビルダー | ✅ |
| `KafkaAdminService`        | DLQ トピック作成などの管理操作  | ✅      |
| `AvroOperationRetrySettings`| Avro操作ごとのリトライ設定     | ✅      |
| `AvroRetryPolicy`          | リトライ回数や遅延などの詳細ポリシー | ✅  |

`KsqlDslOptions.DlqTopicName` は既定で `"dead.letter.queue"` です【F:src/Configuration/KsqlDslOptions.cs†L31-L34】。

<a id="fluent-api-list"></a>
### Fluent API 一覧

| メソッド | 説明 |
|----------|------|
| `Entity<T>(readOnly = false, writeOnly = false)` | エンティティ登録とアクセスモード指定 |
| `.HasKey(expr)` | 主キーを指定する（必須） |
| `.WithTopic(name, partitions = 1, replication = 1)` | トピック名と構成を指定 |
| `.AsStream()` | ストリーム型として登録 |
| `.AsTable(topicName = null, useCache = true)` | テーブル型として登録 |
| `.WithManualCommit()` | 手動コミットモード有効化 |
| `.WithDecimalPrecision(prop, precision, scale)` | Decimal 精度を設定 |
| `.WithPartitions(partitions)` | パーティション数を設定（拡張） |
| `.WithReplicationFactor(replicationFactor)` | レプリケーション係数を設定（拡張） |
| `.WithPartitioner(partitioner)` | カスタムパーティショナーを指定（拡張） |
| `.HasQuery(query)` | LINQ クエリからモデルを構築 |
| `DefineQuery<TSource, TTarget>(query)` | ソースとターゲットを指定したクエリ定義 |

<a id="fluent-api-guide"></a>
## Fluent API ガイドライン

POCO モデルを Fluent API で構成する際の設計指針と移行フローをまとめます。属性ベースからの移行後は
`IEntityBuilder<T>` を用いて宣言的に設定を行います。

### 1. 基本方針
 - 旧 `[Topic]` などの属性は廃止され、設定は Fluent API へ統合されました。
- `HasKey` は必須呼び出しとし、複合キーも `HasKey(e => new { e.A, e.B })` で定義します。
- エンティティ登録時は `readonly` `writeonly` `readwrite` の 3 種類で役割を指定し、未指定時は `readwrite` とみなします。

### 2. 推奨記述例
```csharp
class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

void OnModelCreating(ModelBuilder builder)
{
    builder.Entity<Order>(writeOnly: true)
        .HasKey(o => o.Id)
        .WithTopic("orders")
        .WithDecimalPrecision(o => o.Amount, precision: 18, scale: 2);
}
```
旧 `[Topic]` や `DecimalPrecision` 属性を使用せずにトピックや精度を設定できます。

### 3. 既存 POCO → Fluent API 移行フロー
1. POCO から属性を削除し、純粋なデータクラスとする。
2. `OnModelCreating` で `builder.Entity<T>()` を呼び出し、`HasKey` と各種設定を定義。
3. テストを実行してキー順序やトピック設定が正しいか確認する。
   旧属性に関する詳細は `docs/namespaces/core_namespace_doc.md` を参照してください。

### 4. MappingManager との連携
`MappingManager` を利用して key/value を抽出する例です。詳細は `docs/architecture/key_value_flow.md` を参照してください。
```csharp
var ctx = new MyKsqlContext(options);
var mapping = ctx.MappingManager;
var entity = new Order { Id = 1, Amount = 100 };
var (key, value) = mapping.ExtractKeyValue(entity);
await ctx.AddAsync(entity);
```
#### ベストプラクティス
- エンティティ登録は `OnModelCreating` 内で一括定義する。
- `MappingManager` を毎回 `new` しない。DI コンテナで共有し、モデル登録漏れを防ぐ。

### 5. 追加検討事項
- `WithTopic` のオプション拡張方法（パーティション数など）の公開方法を検討中。
- MappingManager のキャッシュ戦略（スレッドセーフな実装範囲）を確定する必要あり。

### 6. サンプル実装での気づき
- `AddSampleModels` 拡張で `MappingManager` への登録をまとめると漏れ防止になる。
- 複合キーは `Dictionary<string, object>` として抽出されるため、型安全ラッパーの検討余地あり。
- 複数エンティティを登録するヘルパーがあると `OnModelCreating` の記述量を抑えられる。

### 7. AddAsync 統一に伴うポイント
- メッセージ送信 API は `AddAsync` に一本化した。旧 `ProduceAsync` は廃止予定。
- LINQ クエリ解析から `MappingManager.ExtractKeyValue()` を経由し `AddAsync` を呼び出す流れをサンプル化。
- 詳細なコード例は [architecture/query_to_addasync_sample.md](architecture/query_to_addasync_sample.md) を参照。

## エラーハンドリング

| API / Enum                 | 説明                           | 実装状態 |
|----------------------------|--------------------------------|---------|
| `ErrorAction` (Skip/Retry/DLQ) | エラー時の基本アクション    | ✅      |
| `ErrorHandlingPolicy`      | リトライ回数やカスタムハンドラ設定を保持 | ✅ |
| `ErrorHandlingExtensions`  | `.OnError()` `.WithRetryWhen()` 等の拡張 | ✅ |
| `DlqProducer` / `DlqEnvelope` | DLQ 送信処理               | ✅      |
| `DlqTopicConfiguration`    | DLQ トピックの保持期間等を指定 | ✅      |

## 状態監視・内部機構

| API                         | 説明                             | 実装状態 |
|-----------------------------|----------------------------------|---------|
| `ReadyStateMonitor`         | トピック同期状態の監視           | ✅      |
| `CacheBinding`         | Kafka トピックと Cache の双方向バインディング | ✅ |
| `SchemaRegistryClient`      | スキーマ管理クライアント        | ✅      |
| `ResilientAvroSerializerManager` | Avro操作のリトライ管理     | ✅      |

| `WindowFinalizationManager` | Window最終化処理のタイマー管理  | ✅      |

## 各 API の備考

- `IEventSet<T>.WithRetry()` の実装例は `EventSet.cs` にあります【F:src/EventSet.cs†L238-L258】。
- `OnError` の拡張は `EventSetErrorHandlingExtensions.cs` で提供されています【F:src/EventSetErrorHandlingExtensions.cs†L8-L20】。
- 手動コミットの利用例は [manual_commit.md](old/manual_commit.md) を参照してください。
- `StartErrorHandling()` → `.Map()` → `.WithRetry()` の流れで細かいエラー処理を構築できます。
- `AvroOperationRetrySettings` で SchemaRegistry 操作のリトライ方針を制御します【F:src/Configuration/Options/AvroOperationRetrySettings.cs†L8-L33】。

