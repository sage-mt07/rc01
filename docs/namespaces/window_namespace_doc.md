# Kafka.Ksql.Linq.Window Namespace 責務ドキュメント

## 概要
ストリーミングデータの時間窓（Window）処理とその確定（Finalization）を担当する名前空間。リアルタイムデータストリームに対して時間ベースの集約処理を実行し、結果を永続化する。

## サブ名前空間構造

### Kafka.Ksql.Linq.Window.Finalization
ウィンドウの確定処理とその結果の永続化を担当。

---

## 主要コンポーネント責務

### 1. WindowFinalizationManager
**責務**: ウィンドウ確定処理の統括管理
- 複数のWindowProcessorを管理
- タイマーベースでの定期的な確定処理実行
- エンティティタイプごとのウィンドウ設定登録

**主要メソッド**:
- `RegisterWindowProcessor<T>()`: エンティティタイプのウィンドウ処理登録
- `ProcessWindowFinalization()`: 定期確定処理実行

### 2. WindowProcessor<T>
**責務**: 特定エンティティタイプのウィンドウ処理
- イベントの時間窓への振り分け
- 猶予期間経過後のウィンドウ確定
- 集約データの生成とKafka最終トピックへの送信
- 古いウィンドウの自動クリーンアップ

**主要メソッド**:
- `AddToWindow()`: イベントをウィンドウに追加
- `ProcessFinalization()`: 確定可能なウィンドウの処理
- `FinalizeWindow()`: 個別ウィンドウの確定処理

### 3. WindowFinalConsumer
**責務**: 確定されたウィンドウデータの消費と永続化
- 最終トピックからの確定メッセージ消費
- RocksDBを使用した永続化
- 重複排除（同一キーの最初のメッセージのみ保持）
- 履歴データの検索機能

**主要メソッド**:
- `SubscribeToFinalizedWindows()`: 最終トピック購読
- `GetFinalizedWindow()`: 特定ウィンドウの取得
- `GetFinalizedWindowsInRange()`: 期間指定での取得

---

## データ構造

### WindowConfiguration<T>
**責務**: ウィンドウ処理の設定管理
```csharp
- TopicName: 対象トピック名
- Windows: ウィンドウサイズ配列（分）
- GracePeriod: 猶予期間（デフォルト3秒）
- RetentionHours: 保持時間（デフォルト24時間）
- AggregationFunc: 集約関数（デフォルト: イベント数）
- FinalTopicProducer: 最終トピック送信用
```

### WindowFinalMessage
**責務**: 確定されたウィンドウデータの表現
```csharp
- WindowKey: ウィンドウ識別キー
- WindowStart/End: ウィンドウ時間範囲
- WindowMinutes: ウィンドウサイズ
- EventCount: イベント数
- AggregatedData: 集約結果
- FinalizedAt: 確定時刻
- PodId: 処理ポッド識別子
```

### WindowState<T>
**責務**: ウィンドウの実行時状態管理
```csharp
- WindowStart/End: 時間範囲
- Events: 格納イベントリスト
- IsFinalized: 確定状態
- LastUpdated: 最終更新時刻
- Lock: 同期制御オブジェクト
```

---

## 処理フロー

### 1. ウィンドウデータ追加
```
イベント受信 → 時間窓計算 → WindowState作成/更新 → イベント追加
```

### 2. ウィンドウ確定
```
タイマー実行 → 確定対象判定 → 集約処理 → 最終トピック送信 → 状態更新
```

### 3. 確定データ消費
```
最終トピック消費 → 重複チェック → RocksDB保存 → ハンドラー実行
```

---

## 主要な設計パターン

### 時間窓計算
- 時刻を分単位で丸めてウィンドウ開始時刻を計算
- 複数のウィンドウサイズに対応（例：5分、15分、60分）

### 重複排除
- 同一WindowKeyに対して最初のメッセージのみ処理
- 分散環境での複数PODからの重複送信に対応

### 永続化戦略
- インメモリ（高速アクセス）+ RocksDB（永続化）
- 段階的フォールバック：メモリ → RocksDB → null

### エラーハンドリング
- 確定処理失敗時の再試行（IsFinalized フラグリセット）
- タイムアウト制限（30秒）での並列処理

---

## 外部依存関係

- **IKafkaProducer**: 最終トピックへのメッセージ送信
- **StreamizCache**: 確定データの永続化
- **KeyAttribute**: エンティティキー特定（リフレクション使用）

---

## 注意点

1. **KeyAttribute未定義エンティティ**: HashCode使用でキー生成
2. **メモリ使用量**: RetentionHours設定での自動クリーンアップ
3. **並行処理**: WindowStateでのlockベース同期制御
4. **タイマー精度**: デフォルト1秒間隔での確定処理チェック