> **Removed: このnamespaceはCacheに統合され廃止されました。cache_namespace_doc.mdを参照してください。**

# Kafka.Ksql.Linq.StateStore Namespace 責務ドキュメント

## 概要
StateStoreはKafkaストリーム処理における状態管理機能を提供するnamespaceです。RocksDBベースの永続化ストレージとメモリキャッシュによる高速アクセスを実現し、ウィンドウ処理やKTable準拠の状態同期機能を提供します。

## 主要責務

### 1. 状態ストア管理 (Core)
- **IStateStore<TKey, TValue>**: 状態ストアの基本インターフェース
- **RocksDbStateStore<TKey, TValue>**: RocksDB実装（メモリ+ファイル永続化）
- キー・バリューペアの CRUD 操作
- フラッシュ・クローズによる安全な永続化

### 2. 設定管理 (Configuration)
- **StateStoreOptions/Configuration**: ストア設定（キャッシュ有効化、ベースディレクトリ等）
- **StoreTypes**: サポートするストアタイプ定数（RocksDb）

### 3. 管理機能 (Management)
- **StateStoreManager**: エンティティタイプ別ストア生成・管理
- **IStateStoreManager**: 管理インターフェース
- 複数ストアのライフサイクル管理

### 4. ウィンドウ処理 (Extensions)
- **WindowExtensions**: EntitySet → WindowedEntitySet変換
- **WindowedEntitySet<T>**: ウィンドウ集約機能付きEntitySet
- **IWindowedEntitySet<T>**: ウィンドウ処理インターフェース
- **ReadCachedWindowSet<T>**: 確定済みウィンドウデータ読み取り
- **WindowFinalizedExtensions**: 確定データ利用拡張

### 5. Kafka統合 (Integration)
- **TopicStateStoreBinding<T>**: KafkaトピックとStateStoreの双方向同期
- **StateStoreBindingManager**: 複数バインディングの管理・ヘルスチェック
- **BindingHealthStatus**: バインディング状態監視

### 6. Ready状態監視 (Monitoring)
- **ReadyStateMonitor**: Consumer lag監視によるReady状態判定
- **ReadyStateInfo**: 詳細な同期状態情報
- **ReadyStateChangedEventArgs**: Ready状態変更イベント
- **LagUpdatedEventArgs**: Lag更新イベント

### 7. KsqlContext統合
- **EventSetWithStateStore<T>**: StateStore機能付きEntitySet実装
- **KafkaContextStateStoreExtensions**: KsqlContextへのStateStore統合

## 主要な処理フロー

### ストア作成・初期化
```
KsqlContext → StateStoreManager → RocksDbStateStore
```

### ウィンドウ処理
```
EntitySet.Window(minutes) → WindowedEntitySet → StateStore保存
```

### Kafka同期
```
TopicStateStoreBinding → Consumer → StateStore更新 → Ready状態監視
```

### 確定データ利用
```
WindowedEntitySet.UseFinalized() → ReadCachedWindowSet → StateStore読み取り
```

## 設計原則

1. **抽象化**: IStateStoreインターフェースによる実装隠蔽
2. **拡張性**: 新しいストア実装の追加容易性
3. **パフォーマンス**: メモリキャッシュ + 選択的永続化
4. **堅牢性**: エラーハンドリング・自動再試行・ヘルスチェック
5. **監視可能性**: Ready状態・Lag・ヘルス情報の可視化

## 注意点

- RocksDbStateStoreは現在メモリベース実装（永続化は部分的）
- Key再構築処理が簡略化されており、本格運用時は要改良
- Consumer取得にリフレクション使用（実装依存）

このnamespaceはKafka StreamsのStateStore概念をC#で実現し、リアルタイムストリーム処理における状態管理の基盤を提供します。