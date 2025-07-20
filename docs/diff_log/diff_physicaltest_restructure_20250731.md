# 差分履歴: physicaltest_restructure

🗕 2025-07-20
🧐 作業者: 詩音

## 差分タイトル
物理テストコードのディレクトリ再編実施

## 変更理由
- テスト責務を明確化し、環境依存部分を分離するため

## 追加・修正内容（反映先: oss_design_combined.md）
- テストクラスを `tests/Connectivity`, `tests/KsqlSyntax`, `tests/ApiSamples` へ移動
- 環境ヘルパー(TestEnvironment等)を public に変更し `physicalTests` に残置
- `tests` プロジェクトから `physicalTests` を参照

## 参考文書
- `diff_physicaltest_restructure_20250720.md`
