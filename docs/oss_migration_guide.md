# OSS Migration Guide: Attributes to Fluent API

🗕 2025年7月19日（JST）
🧐 作業者: 広夢（戦略広報AI）

このドキュメントでは、旧バージョンで使用していた属性ベースのエンティティ設定から、
Fluent API へ移行する方法をまとめます。実コード例を交え、よくある疑問点への FAQ を
掲載しました。OSS 利用者がスムーズに新 API を使えるよう、本資料を参照してください。

## 旧→新 1:1 マッピング表

| 旧Attribute | 新API メソッド | 備考 |
|-------------|---------------|------|
| `TopicAttribute` | `WithTopic()` | トピック名と構成を指定 |
| `KsqlStreamAttribute` | `AsStream()` | ストリーム定義に変換 |
| `KsqlTableAttribute` | `AsTable()` | テーブル定義に変換 |
| `DecimalPrecisionAttribute` | `WithDecimalPrecision()` | 精度/スケールを指定 |
| `AvroTimestampAttribute` | *(属性廃止)* | DateTime/DateTimeOffset プロパティを1つ定義 |
| `DateTimeFormatAttribute` | *(なし)* | フォーマット変換は不要 |
| `KafkaIgnoreAttribute` | *(なし)* | Fluent API では不要なプロパティは定義しない |

## 移行ステップ最小例

```csharp
// 旧API
[Topic("payments")]
public class Payment
{
    [DecimalPrecision(18, 2)]
    public decimal Amount { get; set; }
}

// 新API
class Payment
{
    public decimal Amount { get; set; }
}

void Configure(ModelBuilder builder)
{
    builder.Entity<Payment>()
        .WithTopic("payments")
        .WithDecimalPrecision(p => p.Amount, precision: 18, scale: 2);
}
```

### 属性からFluent APIへの差分例

属性利用時とFluent API 利用時の最小構成の違いを示します。`Configure` メソッドで
ビルダーを呼び出す点が主な変更箇所です。

```diff
[Topic("payments")]
-public class Payment
-{
-    [DecimalPrecision(18, 2)]
-    public decimal Amount { get; set; }
-}
+public class Payment
+{
+    public decimal Amount { get; set; }
+}
+
+void Configure(ModelBuilder builder)
+{
+    builder.Entity<Payment>()
+        .WithTopic("payments")
+        .WithDecimalPrecision(p => p.Amount, precision: 18, scale: 2);
+}
+```


### POCO から `HasKey` への移行例

旧 `[Key]` 属性を利用していた場合は、Fluent API の `HasKey` メソッドへ置き換えます。

```csharp
// 旧API
[Key]
public class Order
{
    public int Id { get; set; }
}

// 新API
class Order
{
    public int Id { get; set; }
}

void Configure(ModelBuilder builder)
{
    builder.Entity<Order>()
        .HasKey(o => o.Id);
}
```

### KsqlContext 新旧 API 比較（抜粋）

| 機能 | 旧 API | 新 API |
|------|--------|--------|
| エンティティ登録 | `context.Register<T>()` | `builder.Entity<T>()` |
| トピック設定 | `[Topic("name")]` 属性 | `WithTopic("name")` メソッド |
| 主キー指定 | `[Key]` 属性 | `HasKey(expr)` メソッド |

## FAQ

**Q. 既存コードから属性を削除するタイミングは？**
A. Fluent API での設定を確認でき次第、属性は削除してください。属性が残ったままでも
ビルドエラーにはなりませんが、将来の互換性を考慮し早めの切り替えを推奨します。

**Q. `AvroTimestamp` 属性が無くなった場合、タイムスタンプはどう決める？**
A. `DateTime` もしくは `DateTimeOffset` 型のプロパティを一つだけ定義し、
`WindowedEntitySet` などの検証で自動的に認識されます。

**Q. 既存トピック名はどこで指定する？**
A. `builder.WithTopic("orders")` のように `OnModelCreating` 内で指定してください。

さらなる疑問は [core_namespace_redesign_plan.md](core_namespace_redesign_plan.md)
を参照してください。
