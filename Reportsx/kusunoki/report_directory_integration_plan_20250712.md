# レポート保存ディレクトリ統合案
作成日: 2025-07-12 (JST)
作成者: 楠木

## 現状の課題
- `Reportsx/` と `reports/` にレポートが分散し、参照場所が分かりにくい。
- 履歴の整理が煩雑で、更新漏れが発生しやすい。

## 統合案
1. `Reportsx/` をメインの保存場所とし、メンバー別ディレクトリを維持。
2. `reports/` 配下の既存ファイルはアーカイブとして残し、新規レポートはすべて `Reportsx/` へ集約。
3. 共通フォーマットを整備し、ファイル名に日付と担当者を含める (`YYYYMMDD_{name}_report.md` 等)。
4. READMEに統合方針を追記し、新旧ディレクトリの役割を明確化。

## 次のステップ
- 天城へ案を提出し承認を得る。
- 承認後、既存レポートの移動スクリプトを作成し、全員へ周知する。
