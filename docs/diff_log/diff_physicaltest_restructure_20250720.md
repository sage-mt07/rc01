# 差分履歴: physicaltest_restructure

🗕 2025-07-20
🧐 作業者: 詩音

## 差分タイトル
物理テストおよびサンプルコードの責務整理計画

## 変更理由
- Kafka疎通確認、ksqlDB構文検証、DSL利用例の3段階を明確に分離するため
- 既存テストが複数の目的を混在させており、失敗時の原因特定が難しかった

## 追加・修正内容（反映先: oss_design_combined.md）
- Kafka初期疎通テストを `tests/Connectivity` 以下に新設
- ksqlDB構文レベル検証を `tests/KsqlSyntax` ディレクトリへ分離
- DSL APIサンプルを `samples/api-tests` にまとめ、関連テストは `tests/ApiSamples` で実行
- `physicalTests` ディレクトリは環境依存処理と共通ヘルパーのみを保持
- 重複テストクラスを削除し責務単一化を図る

## 参考文書
- `features/test_env_review/instruction.md`
- `features/dummy_flag_test/instruction.md`
