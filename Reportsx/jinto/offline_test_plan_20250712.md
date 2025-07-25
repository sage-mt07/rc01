# テスト環境オフライン化・実行時間短縮策（ドラフト）
作成日: 2025-07-12 (JST)
作成者: 迅人・詩音

## 背景
CI環境で外部依存の取得が制限されるため、テスト実行が不安定になっている。また物理テストの実行時間も長い。

## 対応策案
1. **依存パッケージのローカルキャッシュ**: 必要なNuGetパッケージを事前に保存し、オフラインでも `dotnet restore` 可能にする。
2. **Dockerイメージの共通化**: ksqlDB/Kafka を含むテスト用イメージを作成し、初期化コストを削減。
3. **テストケース整理**: 重複テストや時間のかかる物理テストを分類し、並列実行を検討。
4. **ログ収集の自動化**: 失敗時のみ詳細ログを出力する設定を導入し、解析時間を短縮。

## 次のステップ
- 詳細案を詩音とすり合わせ、7/14までに全体へ共有する資料をまとめる。
