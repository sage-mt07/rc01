# Core Namespace ドキュメント整理方針

## 背景
`architecture_diff_20250711.md` では、`KeyAttribute` を中心としたPOCO属性定義が現仕様と乖離していると指摘されています。再設計方針は `architecture_overview.md` に統合済みで、現在は LINQ 式によるキー指定が標準となっています。現行実装では `EntityModelBuilder.HasKey` を通じて LINQ 式でキーを指定しており、属性は既に利用していません。

## 主要変更点
1. **ドキュメント修正**: `docs/namespaces/core_namespace_doc.md` および関連資料から `KeyAttribute` に関する説明を削除し、LINQ式によるキー指定を明記する。
2. **サンプルコード更新**: `examples/` などに残る KeyAttribute 使用例を削除し、`HasKey()` 呼び出し例へ置き換える。
3. **Deprecated表記**: もし `KeyAttribute` クラスがソースに残っていれば `Obsolete` 属性を付与し将来削除を明示する。

## 対応ファイル
- `docs/namespaces/core_namespace_doc.md`
- `docs/namespaces/window_namespace_doc.md`
- `examples/*` （検索して KeyAttribute 記述を修正）

## 備考
属性ベース定義を前提としたチュートリアル類も合わせて修正予定です。コード上は既に `KeyAttribute` が存在しないため、ドキュメント更新が中心となります。
