using System;
using System.Net;
using System.Net.Http;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Tests.Integration;

internal static class TestEnvironment
{
    private const string SchemaRegistryUrl = "http://localhost:8088";
    private const string KsqlDbUrl = "http://localhost:8081";
    private const string KafkaBootstrapServers = "localhost:9092";
    private const string DlqTopic = "dead.letter.queue";
    private static readonly HttpClient Http = new();
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("TestEnvironment");
    private static readonly string[] ExtraSubjects = new[]
    {
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+ordervalue-key",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+ordervalue-value",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+customer-key",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+customer-value",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+eventlog-key",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+eventlog-value",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+nullableorder-key",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+nullableorder-value",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+nullablekeyorder-key",
        "kafka.ksql.linq.tests.integration.dummyflagschemarecognitiontests+nullablekeyorder-value"
    };

    internal static KsqlContext CreateContext()
    {
        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = SchemaRegistryUrl }
        };

        return new AdminContext(options);
    }

    internal class AdminContext : KsqlContext
    {
        public AdminContext() : base(new KsqlDslOptions()) { }
        public AdminContext(KsqlDslOptions options) : base(options) { }
        protected override bool SkipSchemaRegistration => false;

        public Task DropTableAsync(string tableName, bool deleteTopic = true)
        {
            var ddl = $"DROP TABLE IF EXISTS {tableName.ToUpperInvariant()}" +
                      (deleteTopic ? " DELETE TOPIC;" : ";");
            return ExecuteStatementAsync(ddl);
        }

        public Task DropStreamAsync(string streamName, bool deleteTopic = true)
        {
            var ddl = $"DROP STREAM IF EXISTS {streamName.ToUpperInvariant()}" +
                      (deleteTopic ? " DELETE TOPIC;" : ";");
            return ExecuteStatementAsync(ddl);
        }
    }

    /// <summary>
    /// テスト開始時の初期化処理
    /// </summary>
    public static async Task SetupAsync()
    {
        // connectivity checks
        await EnsureServicesAvailableAsync();

        // ensure DLQ topic and clean schemas
        await EnsureDlqTopicExistsAsync();

        await TryDeleteSubjectAsync("customers-value");
        foreach (var table in TestSchema.AllTopicNames)
        {
            foreach (var suffix in new[] { "-value", "-key" })
            {
                await TryDeleteSubjectAsync($"{table}{suffix}");
            }
        }
        foreach (var subject in ExtraSubjects)
        {
            await TryDeleteSubjectAsync(subject);
        }

        // create required stream/table objects
        await using var ctx = CreateContext();
        await ctx.ExecuteStatementAsync(
            "CREATE STREAM IF NOT EXISTS source (id INT) WITH (KAFKA_TOPIC='source', VALUE_FORMAT='AVRO', PARTITIONS=1);"
        );

        foreach (var ddl in TestSchema.GenerateTableDdls())
        {
            await ctx.ExecuteStatementAsync(ddl);
        }

        await ValidateSchemaRegistrationAsync();
    }

    /// <summary>
    /// テスト終了時の後処理
    /// </summary>
    public static async Task TeardownAsync()
    {
        await using (var ctx = CreateContext())
        {
            try
            {
                if (ctx is AdminContext admin)
                {
                    await admin.DropStreamAsync("source");
                    foreach (var table in TestSchema.AllTableNames)
                    {
                        await admin.DropTableAsync(table);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to drop objects");
            }
        }

        // remove registered schemas to ensure a clean state
        foreach (var table in TestSchema.AllTopicNames)
        {
            foreach (var suffix in new[] { "-value", "-key" })
            {
                var subject = $"{table}{suffix}";
                await TryDeleteSubjectAsync(subject);
            }
        }
        foreach (var subject in ExtraSubjects)
        {
            await TryDeleteSubjectAsync(subject);
        }
        await TryDeleteSubjectAsync("customers-value");
    }

    /// <summary>
    /// 従来のResetは Teardown->Setup の順で実行する
    /// </summary>
    public static async Task ResetAsync()
    {
        // This helper wipes topics and schemas for every test run.
        // Avoid using it in production systems to prevent data loss.
        await TeardownAsync();
        await SetupAsync();
    }

    private static async Task TryDeleteSubjectAsync(string subject)
    {
        try
        {
            var resp = await Http.DeleteAsync($"{SchemaRegistryUrl}/subjects/{subject}");
            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.NotFound)
            {
                Logger.LogWarning("Failed to delete schema {Subject}: {StatusCode}", subject, resp.StatusCode);
            }
            await Task.Delay(200); // wait a bit for schema registry to propagate deletions
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete schema {Subject}", subject);
        }
    }

    private static async Task EnsureServicesAvailableAsync()
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaBootstrapServers }).Build();
            var meta = admin.GetMetadata(TimeSpan.FromSeconds(10));
            if (meta.Brokers.Count == 0)
                throw new InvalidOperationException("Kafka unreachable");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to Kafka");
            throw new InvalidOperationException("Kafka connectivity check failed", ex);
        }

        try
        {
            await using var ctx = CreateContext();
            var r = await ctx.ExecuteStatementAsync("SHOW TOPICS;");
            if (!r.IsSuccess)
                throw new InvalidOperationException("ksqlDB unreachable");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to ksqlDB");
            throw new InvalidOperationException("ksqlDB connectivity check failed", ex);
        }

        try
        {
            var resp = await Http.GetAsync($"{SchemaRegistryUrl}/subjects");
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException("SchemaRegistry unreachable");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to Schema Registry");
            throw new InvalidOperationException("SchemaRegistry connectivity check failed", ex);
        }
    }

    private static async Task EnsureDlqTopicExistsAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaBootstrapServers }).Build();
        var meta = admin.GetMetadata(TimeSpan.FromSeconds(10));
        if (meta.Topics.Any(t => t.Topic == DlqTopic && !t.Error.IsError))
            return;

        try
        {
            await admin.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = DlqTopic, NumPartitions = 1, ReplicationFactor = 1 }
            });
        }
        catch (CreateTopicsException ex)
        {
            var result = ex.Results.FirstOrDefault(r => r.Topic == DlqTopic);
            if (result?.Error.Code != ErrorCode.TopicAlreadyExists)
                Logger.LogError("Failed to create DLQ topic: {Reason}", result?.Error.Reason);
        }
    }

    private static async Task ValidateSchemaRegistrationAsync(int attempts = 5, int delayMs = 1000)
    {
        var expected = TestSchema.AllTopicNames
            .SelectMany(n => new[] {$"{n}-value", $"{n}-key"})
            .Concat(new[] { "source-value" })
            .ToArray();

        for (var i = 0; i < attempts; i++)
        {
            var resp = await Http.GetAsync($"{SchemaRegistryUrl}/subjects");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var subjects = System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();

            if (expected.All(subjects.Contains))
                return;

            await Task.Delay(delayMs);
        }

        var respFinal = await Http.GetAsync($"{SchemaRegistryUrl}/subjects");
        respFinal.EnsureSuccessStatusCode();
        var finalJson = await respFinal.Content.ReadAsStringAsync();
        var finalSubjects = System.Text.Json.JsonSerializer.Deserialize<string[]>(finalJson) ?? Array.Empty<string>();
        var missing = expected.Where(e => !finalSubjects.Contains(e));
        throw new InvalidOperationException($"Schema not registered: {string.Join(", ", missing)}");
    }
}

