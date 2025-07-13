# 差分履歴: raw_kafka_consumer_updates

🗕 2025年7月14日（JST）
🧐 作業者: naruse

## 差分タイトル
RawKafkaConsumer の基本操作を見直し CommitAsync を追加

## 変更理由
Subscribe を毎回実行していたため不必要な再購読が発生していた。オフセット操作を行うため CommitAsync も必要となった。

## 追加・修正内容（反映先: oss_design_combined.md）
- IRawKafkaConsumer インターフェースに CommitAsync を追加
- RawKafkaConsumer.ConsumeAsync から Subscribe 呼び出しを削除し、async メソッドとして実装
- RawKafkaConsumer に CommitAsync 実装を追加

## 参考文書
- `Messaging` ディレクトリの実装ガイド
