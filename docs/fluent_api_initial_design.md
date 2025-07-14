# Fluent API 初期設計ガイド

🗕 2025年7月13日（JST）
🧐 作業者: 広夢・楠木

本ドキュメントでは、POCO モデルを Fluent API で構成する際の設計ガイドラインと、移行フローの一例をまとめる。コア属性廃止後の推奨記述例や MappingManager 連携パターンも示す。

## 1. 基本方針
- `docs/core_namespace_redesign_plan.md` で示されたとおり、`TopicAttribute` などの属性は削除予定。
- `IEntityBuilder<T>` を介してキーやトピックなどを宣言的に設定する。
- `HasKey` は必須呼び出しとし、複合キーも `HasKey(e => new { e.A, e.B })` で定義する。
- エンティティ登録時に `readOnly` または `writeOnly` フラグを指定でき、未指定時は両用となる。

## 2. 推奨 Fluent API 記述例
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
上記により、旧 `[Topic]` や `DecimalPrecision` 属性を使用せずにトピックや精度を設定できる。

## 3. 既存 POCO → Fluent API 移行フロー
1. POCO から属性を削除し、純粋なデータクラスとする。
2. `OnModelCreating` で `builder.Entity<T>()` を呼び出し、`HasKey` と各種設定を定義。
3. テストを実行してキー順序やトピック設定が正しいか確認する。

参考として、`docs/oss_migration_guide.md` では属性とメソッドの 1:1 対応表が記載されている。

## 4. MappingManager との連携
以下は `MappingManager` を利用して key/value を抽出する例である。詳細は `docs/architecture/key_value_flow.md` を参照。
```csharp
var ctx = new MyKsqlContext(options);
var mapping = ctx.MappingManager;
var entity = new Order { Id = 1, Amount = 100 };
var (key, value) = mapping.ExtractKeyValue(entity);
await ctx.AddAsync(entity);
```
### ベストプラクティス
- エンティティ登録は `OnModelCreating` 内で一括定義する。
- `MappingManager` を毎回 `new` しない。DI コンテナで共有し、モデル登録漏れを防ぐ。

## 5. 追加検討が必要な論点
- `WithTopic` のオプション拡張方法（パーティション数など）をどう公開するか要議論。
- MappingManager のキャッシュ戦略（スレッドセーフな実装範囲）を確定する必要あり。

以上。

## 6. サンプル実装での気づき
- `AddSampleModels` 拡張で `MappingManager` への登録をまとめると漏れ防止になる。
- 複合キーは `Dictionary<string, object>` として抽出されるため、型安全ラッパーの検討余地あり。
- 複数エンティティを登録するヘルパーがあると `OnModelCreating` の記述量を抑えられる。

## 7. AddAsync 統一に伴うポイント
- メッセージ送信 API は `AddAsync` に一本化した。旧 `ProduceAsync` は廃止予定。
- LINQ クエリ解析から `MappingManager.ExtractKeyValue()` を経由し `AddAsync` を呼び出す流れをサンプル化。
- 詳細なコード例は [architecture/query_to_addasync_sample.md](architecture/query_to_addasync_sample.md) を参照。
