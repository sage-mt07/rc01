# Kafka.Ksql.Linq.Cache Namespace 責務ドキュメント

## 概要
`Kafka.Ksql.Linq.Cache` はテーブル読み取りのためのキャッシュ機能を提供する新しい namespace です。
Streamiz.Kafka.Net の `KafkaStream` を利用し、RocksDB ベースの状態ストアを内部で管理します。
トピックとのデータ同期は Avro 形式の key/value で行われ、型情報は `Mapping` namespace が生成した型を利用します。

## 主要責務
1. **キャッシュ管理**
   - Streamiz の `KafkaStream` インスタンスを保持し、テーブルデータをローカル RocksDB に保持
   - 生成時に `KafkaStream` のステートが `RUNNING` になるまで待機
   - 稼働中に `RUNNING` 以外へ遷移した場合は警告ログを出力
2. **データ取得 API**
   - `ToListAsync()` などデータ取得時に `RUNNING` でなければ例外を返す
   - 通常は `IKsqlContext.Set<T>()` から透過的に利用される
3. **設定連携**
   - `appsettings.json` の `TableCache` セクションからキャッシュ有効フラグ、`BaseDirectory`、`StoreName` などを読み込む
   - `StoreName` を省略した場合はトピック名に基づく名前を自動生成する

このキャッシュは Table 型の EntitySet でデフォルト有効となり、高速な再読込を実現します。

---

## サブ名前空間構造

- `Kafka.Ksql.Linq.Cache.Core` … 基本キャッシュ実装 (`ITableCache<T>`, `RocksDbTableCache<T>`)
- `Kafka.Ksql.Linq.Cache.Integration` … Kafka との同期バインディング (`ConsumerCacheBinding`)
- `Kafka.Ksql.Linq.Cache.Configuration` … `TableCacheOptions` など設定クラス
- `Kafka.Ksql.Linq.Cache.Extensions` … `KsqlContext` への統合拡張

---

## 主要コンポーネント責務

### 1. TableCacheRegistry
**責務**: エンティティタイプごとのキャッシュ生成と `KafkaStream` ライフサイクル管理
- `InitializeCaches()` で設定に基づき `RocksDbTableCache<T>` を生成
- `GetCache<T>()` で既存キャッシュ取得
- `Dispose()` 時に全 `KafkaStream` を停止

### 2. RocksDbTableCache<T>
**責務**: Streamiz の RocksDB ストアを利用したテーブルキャッシュ
- `KafkaStream` をバックグラウンドで起動し、トピックからデータを取り込み
- `State` が `RUNNING` になるまで初期化時に待機
- `GetAll()` / `TryGet(key)` などの基本操作を提供

### 3. ConsumerCacheBinding
**責務**: Kafka Consumer からのストリームを RocksDB キャッシュへ反映
- オフセット管理と再試行処理を担当
- `RUNNING` 以外へ遷移した際は警告ログを発行

### 4. ReadCachedEntitySet<T>
**責務**: `ITableCache<T>` を利用した読み取り専用 `IEntitySet<T>` 実装
- `ToListAsync()` 実行時にキャッシュの `State` を検証
- キャッシュが無効化されている場合は直接 Kafka から取得

### 5. KsqlContextCacheExtensions
**責務**: `KsqlContext` へのキャッシュ登録・取得補助
- `UseTableCache()` で `TableCacheRegistry` を初期化
- `GetTableCache<T>()` で型安全に取得

---

## 処理フロー概要

1. `KsqlContext` 初期化時に `UseTableCache()` を呼び出し、`TableCacheRegistry` を生成
2. `TableCacheRegistry.InitializeCaches()` が `TableCacheOptions` を参照し各キャッシュを起動
3. `ConsumerCacheBinding` がトピック更新を RocksDB へ反映
4. アプリコードから `Set<T>().ToListAsync()` を呼ぶと `ReadCachedEntitySet<T>` が `RocksDbTableCache<T>` からデータを返す

