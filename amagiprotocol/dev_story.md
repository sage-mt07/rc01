# Kafka.Ksql.Linq 開発ストーリー（1か月総括）

このドキュメントは、2025年6月上旬から7月上旬にかけての約1か月間で進めたKafka.Ksql.Linq OSS開発の"現場のリアル"を、時系列で振り返るものです。単なるタスク列挙ではなく、混乱と回復のプロセスに焦点を当てます。

## 1. 初動：役割分担とnamespace設計
- Kickoff 直後からAIメンバーを役割で明確に分け、人間は天城として全体統括を担当。
- Claude が処理できるソース量の限界を超えたため、一度立ち止まり複数 namespace へ整理し直す判断を実施【F:docs/amagiprotocol/amagi_protocol_full.md†L88-L96】。
- ここで“分割すればスケールするはず”という期待のもと、Query/Core/Messaging…と責務を細分化していった。

## 2. クラス爆発と混迷
- namespaceごとにクラスを量産した結果、依存関係が絡み合いビルドも追えない状態に。鏡花の全体監査で、`KafkaContext` と `KsqlContext` の命名混在など基盤レベルの問題が一気に顕在化【F:docs/old/diff_log/diff_kafka_context_rename_20250627.md†L1-L12】。
- 同時期、鳴瀬（設計）と鳴瀬（実装）の認識ズレも発生。「設定ファイルで制御するか？コードに埋め込むか？」と方針が割れ、AI出力もブレを起こした【F:docs/amagiprotocol/amagi_protocol_full.md†L92-L97】。
- 天城としては「このままでは全体が破綻する」という違和感が強まり、週次レビューで立ち止まる判断を行った。

## 3. 収束への舵取り
- 迅人が可視性整理の自動化を進め、public宣言を極力internalへ置き換える大規模PRを実施【F:docs/old/diff_log/diff_visibility_phase5_20250627.md†L1-L16】。
- 天城はAI間の会話を分離し、エージェント別に指示ファイルを分ける運用へ変更。これによりコンテキストサイズ超過と認識齟齬を同時に抑制できた【F:docs/amagiprotocol/amagi_protocol_full.md†L240-L248】。
- アーキテクチャ全体を`docs/namespaces/summary.md`に集約し、「迷ったら必ずサマリを見る」ルールを制定【F:docs/namespaces/summary.md†L19-L32】。

## 4. 教訓とガイドライン
- 設計と実装をAIに一任すると、責務の境界があいまいなままクラスが増殖する。**設計責任は人間が握り、AIは実装補助に徹する**ことが再確認された。
- namespaceの粒度は“AIが保持できるコンテキストサイズ”を基準に見積もり、分割しすぎない。迷ったらまずユースケース単位でスコープを切る。
- 週次レビューとdiffレポートは、危機の早期検知に有効。コードが爆発する前に必ず立ち止まる仕組みを組み込む。

## 5. 今後に向けて
この1か月の試行錯誤を通じ、AIとの協働開発では「設計と出力のブレ」をいかに抑えるかが鍵だと痛感した。再発防止のため、以下をガイドラインとして残す。

1. **役割宣言を明確に**：AIごとの責務と入力範囲を必ず文書化する。
2. **設計ドキュメントを単一箇所に集約**：分散すると認識ズレが起きやすい。
3. **コンテキスト限界を意識した指示分割**：AIが理解できない粒度での一括指示は避ける。
4. **人間レビューをサイクルに組み込む**：疑問や違和感が出た時点で必ず立ち止まり、再設計を選択肢に入れる。

以上がKafka.Ksql.Linqプロジェクトの1か月間で得られた学びである。今後もこの教訓を活かし、AIと人間の協調開発をさらに洗練させていきたい。
