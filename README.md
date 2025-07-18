# Kafka.Ksql.Linq

本OSSはC# Entity Framework/DbContextに着想を得た直感的なLINQスタイルDSLを提供します。



## 特徴
Kafka.Ksql.Linqは、Kafka／ksqlDB向けのクエリを  

C#のLINQスタイルで簡潔かつ直感的に記述できる、Entity Framework風のDSLライブラリです。  

既存のRDB開発経験者でも、Kafkaストリーム処理やKSQL文の記述・運用を  

.NETの慣れ親しんだ形で実現できることを目指しています。

 ⚠️ **注意：本OSSは見た目はEF/LINQ風ですが、実装の本質は「Kafka/KSQL専用DSL」です。  
そのため、通常のEF/LINQのようなWhere/GroupBy等のチェーン式は「アプリ本体で書いてもKSQLに反映されません」。
正しい粒度や集約単位の指定は「Window(x)」拡張メソッドを唯一の正解として採用しています。**

💡 **Key schema に使用できる型は `int` `long` `string` `Guid` のみです。その他の型をキーにしたい場合は、必ずこれらの型へ変換してください。**

## サンプルコード

```
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public class ManualCommitOrder
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}

public class ManualCommitContext : KsqlContext
{
    public ManualCommitContext(KafkaContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ManualCommitOrder>()
            .WithManualCommit();
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // 設定ファイルからKafkaContextOptionsを生成
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var options = KafkaContextOptions.FromConfiguration(configuration);


        // Contextを直接newする
        await using var context = new ManualCommitContext(options);

        var order = new ManualCommitOrder
        {
            OrderId = Random.Shared.Next(),
            Amount = 10m
        };

        await context.Set<ManualCommitOrder>().AddAsync(order);
        await Task.Delay(500);

        await context.Set<ManualCommitOrder>().ForEachAsync(async (IManualCommitMessage<ManualCommitOrder> msg) =>
        {
            try
            {
                Console.WriteLine($"Processing order {msg.Value.OrderId}: {msg.Value.Amount}");
                await msg.CommitAsync();
            }
            catch
            {
                await msg.NegativeAckAsync();
            }
        });
    }
}


```

❌ 誤用例（NG）
⚠️ 注意：本OSSは見た目はEF/LINQ風ですが、「Where/GroupBy」等のLINQチェーンは「アプリ本体」側ではKSQLに一切反映されません。

```
// これはksqldbのストリーム定義には作用しません
await context.Set<ApiMessage>()
    .Where(m => m.Category == "A")    // ← 実際にはフィルタされない
    .GroupBy(m => m.Category)         // ← 集約もksqldb側には伝わらない
    .ForEachAsync(...);
```

✅ 正しいパターン（推奨）

```
// OnModelCreatingなどで、あらかじめストリーム/テーブル＋条件を宣言する
modelBuilder.Entity<ApiMessage>()
    .HasQuery(q => q.Where(m => m.Category == "A").GroupBy(m => m.Category));

// その上で、アプリ側は
await context.Set<ApiMessageFiltered>()
    .ForEachAsync(...);  // ← 事前登録済みストリーム/テーブルにアクセス
```


⚠️ 注意：KSQLのクエリ定義とLINQ式について

このOSSではC#のDSL（POCO＋属性＋OnModelCreating）でストリーム/テーブルの定義やフィルタ・集約が可能ですが、
その内容は裏側でKSQL（CREATE STREAM/TABLE ...）として自動登録されています。

アプリ側で .ForEachAsync() や .ToListAsync() の前に Where/GroupBy など LINQ式を書いても、
ksqldbサーバの本質的なストリーム/テーブル定義には作用しません。

本当に効かせたいフィルタや集約は、必ずOnModelCreating等のDSLで事前登録してください。

複数ウィンドウの集約・推奨パターン
Window(x)拡張メソッドを用いてウィンドウ粒度ごとにデータを扱うことができます。
```
// ✅ Window(x)パターン（唯一の正解・推奨パターン）
await context.Set<OrderCandle>()
    .Window(5)
    .ForEachAsync(...);
```


## Quick Start
1. .NET 6 SDKインストール（dotnet --versionで確認）
2. リポジトリ clone ＆ dotnet restore
3. Kafka/ksqlDB/Schema Registry 起動:
   ```bash
   docker-compose -f tools/docker-compose.kafka.yml up -d
   ```
サンプル実行例（hello-worldなど）

   ```bash
cd examples/hello-world
dotnet run
   ```
送信/受信それぞれの出力を確認

簡易的なセットアップとテスト実行をまとめたスクリプトも用意しています。
```bash
tools/quickstart_integration.sh
```
テストが失敗した場合は [docs/troubleshooting.md](./docs/troubleshooting.md) を参照してください。

Integration テストの実行
事前準備
上記 docker-compose で環境が起動済みであること

Kafka、Schema Registry、ksqlDB を再起動した場合でも、テスト開始時に
`TestEnvironment.ResetAsync()` が実行され、必要な全 Avro スキーマ
(例: `orders-value`) が Schema Registry に再登録されます。
サブジェクト名は **トピック名（小文字）-value/key** 形式で登録されます。
登録に失敗した場合は `KsqlDbFact` 属性によりテストが自動的にスキップされます。

追加の .env や appsettings.json 設定が必要な場合は docs/getting-started.md を参照

テスト実行
   ```bash
dotnet test physicalTests/Kafka.Ksql.Linq.Tests.Integration.csproj --filter Category=Integration
   ```
テストの前提・挙動
テスト開始時に `ResetAsync()` が実行され、必要なストリーム/テーブルの作成と
Avro スキーマ登録をまとめて行います

テスト終了後、DROP/サブジェクト削除が自動実施され、環境がクリーンアップされます

失敗やスキップの原因は logs/ と docker logs で確認できます

Kafka/Schema Registry/ksqlDB をリセットした直後も、同じ `dotnet test` コマンドで
Reset → Setup → Test の順に自動で実行されます。

注意：本番運用ではこのような頻繁なreset/teardownは行いません

トラブルシュート
curl http://localhost:8081/subjects でSchema Registryの状態を確認

NAME_MISMATCH等のエラー時は手動で該当subject削除後に再実行

詳細・応用
開発フロー・運用設計ガイドは docs/dev_guide.md および docs/docs_advanced_rules.md 参照

### 1. インストール
### 2. 設定
### 3. 使用例
###📂  4. サンプルコード

実行可能なサンプルは `examples/` フォルダーにまとまっています。Producer と Consumer をペアで収録しており、各READMEに手順を記載しています。

- [hello-world](./examples/hello-world/) - 最小構成のメッセージ送受信
- [basic-produce-consume](./examples/basic-produce-consume/) - getting-started の基本操作
- [window-finalization](./examples/window-finalization/) - タンブリングウィンドウ集計の確定処理
- [error-handling](./examples/error-handling/) - リトライとエラーハンドリングの基礎
- [error-handling-dlq](./examples/error-handling-dlq/) - DLQ運用を含むエラー処理
- [configuration](./examples/configuration/) - 環境別のログ設定例
- [configuration-mapping](./examples/configuration-mapping/) - appsettings と DSL 設定のマッピング
- [manual-commit](./examples/manual-commit/) - 手動コミットの利用例
- [sqlserver-vs-kafka](./examples/sqlserver-vs-kafka/) - SQL Server 操作との対比
- [api-showcase](./examples/api-showcase/) - 代表的な DSL API の利用例
- [daily-comparison](./examples/daily-comparison/) - 日次集計の簡易サンプル


## 📚 ドキュメント構成ガイド

このOSSでは、利用者のレベルや目的に応じて複数のドキュメントを用意しています。

### 🧑‍🔧 現場担当者向け（運用手順を素早く知りたい方）
| ドキュメント | 内容概要 |
|--|--|
| `docs/getting-started.md` | 基本的なセットアップとサンプル実行手順 |
| `docs/troubleshooting.md` | 典型的なエラー時の対処法まとめ |
| `docs/api_reference.md` | よく使うコマンド・APIリファレンス |
| `docs/physical_test_minimum.md` | 現場での最小テスト手順 |
| `docs/new_member_reference.md` | 新規参加者向けの必読資料一覧と利用フロー |

### 🧑‍🏫 初級〜中級者向け（Kafkaに不慣れな方）
| ドキュメント | 内容概要 |
|--|--|
| `docs/sqlserver-to-kafka-guide.md` | [SQL Server経験者向け：Kafkaベースの開発導入ガイド](./docs/sqlserver-to-kafka-guide.md) |
| `docs/getting-started.md` | [はじめての方向け：基本構成と動作確認手順](./docs/getting-started.md) |

### 🛠️ 上級開発者向け（DSL実装や拡張が目的の方）
| ドキュメント | 内容概要 |
|--|--|
| `docs/dev_guide.md` | [OSSへの機能追加・実装フローと開発ルール](./docs/dev_guide.md) |
| `docs/oss_migration_guide.md` | [属性からFluent APIへの移行手順とFAQ](./docs/oss_migration_guide.md) |
| `docs/namespaces/*.md` | 各Namespace（Core / Messaging 等）の役割と構造 |

### 🏗️ アーキテクト・運用担当者向け（構造や制約を把握したい方）
| ドキュメント | 内容概要 |
|--|--|
| `docs/docs_advanced_rules.md` | [運用設計上の制約、設計判断の背景と意図](./docs/docs_advanced_rules.md) |
| `docs/docs_configuration_reference.md` | [appsettings.json などの構成ファイルとマッピング解説](.docs/docs_configuration_reference.md) |
| `docs/architecture_overview.md` | [全体アーキテクチャ構造と各層の責務定義](./docs/architecture_overview.md) |
| `docs/architecture/query_ksql_mapping_flow.md` | [Query→KsqlContext→Mapping/Serialization 連携仕様](./docs/architecture/query_ksql_mapping_flow.md) |
| `docs/test_guidelines.md` | [ksqlDB仕様準拠のテストガイドライン](./docs/test_guidelines.md) |
| `docs/poco_design_policy.md` | [POCO設計・PK運用・シリアライズ方針](./docs/poco_design_policy.md) |
- ドキュメントの重複・矛盾チェック結果は [docs/duplication_check_20250729.md](./docs/duplication_check_20250729.md) を参照

---
> 本プロジェクトの開発思想・AI協働方法論は[Amagi Protocol統合ドキュメント](./docs/amagiprotocol/amagi_protocol_full.md)、

\> 実運用の流れを簡潔にまとめたダイジェストは[docs/amagiprotocol/README.md](./docs/amagiprotocol/README.md)を参照してください。
\> namespace分割による混乱からの回復までを追ったストーリーは[docs/amagiprotocol/dev_story.md](./docs/amagiprotocol/dev_story.md)にまとめています。

⚠️ `docs/amagiprotocol/` 以下はPM・AI専用の議事録や設計履歴を保存する領域です。現場担当者は通常参照する必要はありません。

運用効率化のため、今後は`docs/pm_ai/`など専用ディレクトリへ移動し、現場向けドキュメントとの区別をより明確にすることを提案します。
