# API Reference (Draft)

この文書は `Kafka.Ksql.Linq` OSS における公開 DSL/API と主要コンポーネントの概要を整理したものです。今後の設計ドキュメントや実装コード、テストコードへの参照基盤として利用します。

## 既定値の参照

- 既定値一覧は [docs_configuration_reference.md](docs_configuration_reference.md) を参照してください。

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
| `.Window().BaseOn<TSchedule>(keySelector, ?openProp, ?closeProp)` | `[ScheduleRange]` 属性、または `openProp`/`closeProp` パラメータで開始/終了を示すスケジュールPOCOに基づきウィンドウを生成 | `IQueryable<T>` | Stream | ✅ |
| `.GroupBy(...)`                | グループ化および集約          | `IEventSet<IGrouping<TKey, T>>`   | Stream/Table  | ✅      |
| `.OnError(ErrorAction)`        | エラー処理方針指定            | `EventSet<T>`                     | Stream        | ✅      |
| `.WithRetry(int)`              | リトライ設定                  | `EventSet<T>`                     | Stream        | ✅      |
| `.StartErrorHandling()`        | エラーチェーン開始            | `IErrorHandlingChain<T>`          | Stream        | ✅      |
| `.WithManualCommit()`          | 手動コミットモード切替        | `IEntityBuilder<T>`               | Subscription  | ✅      |

- `ToList`/`ToListAsync` は Pull Query として実行されます【F:src/Query/Pipeline/DMLQueryGenerator.cs†L27-L34】。
- `WithManualCommit()` を指定しない `ForEachAsync()` は自動コミット動作となります【F:docs/old/manual_commit.md†L1-L23】。
- `OnError(ErrorAction.DLQ)` を指定すると DLQ トピックへ送信されます【F:docs/old/defaults.md†L52-L52】。
- Messaging クラス自体は DLQ 送信処理を持たず、`ErrorOccurred`/`DeserializationError`/`ProduceError` などのイベントを通じて外部で DLQ 送信を行います。
- `.Window().BaseOn<TSchedule>` を用いる場合、バーは `[ScheduleRange]` 属性、または `openProp`/`closeProp` パラメータで示された `Open` ～ `Close` の範囲に含まれるデータのみで構成されます。日足生成で `Close` が 6:30 のときは、6:30 未満のデータが当日の終値として扱われます。
- バーやウィンドウ定義は必ず `KafkaKsqlContext.OnModelCreating` 内で宣言してください。アプリケーション側では定義済みの `Set<T>` を参照するだけです。
- `WithWindow<Rate, MarketSchedule>()` に続けて `.Select<RateCandle>()` を呼び出すことで、レートからバーエンティティを構成できます。
- `WithWindow<TEntity, TSchedule>(windows, timeSelector, rateKey, scheduleKey)` として `timeSelector` 引数でウィンドウを区切る時刻プロパティを明示します。

これらの戻り値型を把握することで、DSLチェーンにおける次の操作を判断しやすくなります。特に `OnError()` や `WithRetry()` は `EventSet<T>` を返すため、続けて `IEventSet` 系メソッドを利用できます。

## 属性 (Attribute) 定義

| 属性                       | 役割                           | 実装状態 |
|----------------------------|--------------------------------|---------|
| `TopicAttribute`           | トピック構成指定               | ❌      |
| `KeyAttribute`             | キー項目指定                   | ❌      |
| `KsqlTableAttribute`       | テーブル情報指定               | ❌      |
| `AvroTimestampAttribute`   | Avro タイムスタンプ列指定      | ❌      |
| `DecimalPrecisionAttribute`| Decimal 精度指定               | ❌      |
| `RetryAttribute`           | (予定) リトライポリシー指定    | ⏳      |
| `KsqlColumnAttribute`      | (予定) 列名マッピング          | ⏳      |
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

