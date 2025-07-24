using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kafka.Ksql.Linq.Application;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class SchemaRegistryResetTests
{
    private static readonly HttpClient Http = new();

    private static bool IsKsqlDbAvailable()
    {
        lock (_sync)
        {
            if (_available)
                return true;

            if (_lastFailure.HasValue && DateTime.UtcNow - _lastFailure.Value < TimeSpan.FromSeconds(5))
                return false;

            const int attempts = 3;
            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    using var ctx = TestEnvironment.CreateContext();
                    var r = ctx.ExecuteStatementAsync("SHOW TOPICS;").GetAwaiter().GetResult();
                    if (r.IsSuccess)
                    {
                        _available = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ksqlDB check attempt {i + 1} failed: {ex.Message}");
                }

                Thread.Sleep(1000);
            }

            _available = false;
            _lastFailure = DateTime.UtcNow;
            return false;
        }
    }

    private static bool _available;
    private static DateTime? _lastFailure;
    private static readonly object _sync = new();

    // Reset 後に全テーブルのスキーマが登録されているか確認
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Setup_ShouldRegisterAllSchemas()
    {
        if (!IsKsqlDbAvailable())
            throw new SkipException("ksqlDB unavailable");

        try
        {
            await TestEnvironment.ResetAsync();

        }
        catch (Exception)
        {

        }

        var subjects = await Http.GetFromJsonAsync<string[]>($"{TestEnvironment.SchemaRegistryUrl}/subjects");
        Assert.NotNull(subjects);

        foreach (var table in TestSchema.AllTopicNames)
        {
            Assert.Contains($"{table}-value", subjects);
            Assert.Contains($"{table}-key", subjects);
        }
        Assert.Contains("source-value", subjects);
    }

    // 既存スキーマを再登録しても成功するか確認
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DuplicateSchemaRegistration_ShouldSucceed()
    {
        if (!IsKsqlDbAvailable())
            throw new SkipException("ksqlDB unavailable");

        try
        {
            await TestEnvironment.ResetAsync();

        }
        catch (Exception)
        {

        }

        var latest = await Http.GetFromJsonAsync<JsonElement>($"{TestEnvironment.SchemaRegistryUrl}/subjects/orders-value/versions/latest");
        var schema = latest.GetProperty("schema").GetString();
        var resp = await Http.PostAsJsonAsync($"{TestEnvironment.SchemaRegistryUrl}/subjects/orders-value/versions", new { schema });
        resp.EnsureSuccessStatusCode();
    }

    // 大文字のサブジェクト名が存在しないことを確認
    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpperCaseSubjects_ShouldNotExist()
    {
        if (!IsKsqlDbAvailable())
            throw new SkipException("ksqlDB unavailable");
        try
        {
            await TestEnvironment.ResetAsync();

        }
        catch (Exception)
        {

        }
        var subjects = await Http.GetFromJsonAsync<string[]>($"{TestEnvironment.SchemaRegistryUrl}/subjects");
        Assert.NotNull(subjects);

        foreach (var table in TestSchema.AllTopicNames)
        {
            Assert.DoesNotContain($"{table.ToUpperInvariant()}-value", subjects);
            Assert.DoesNotContain($"{table.ToUpperInvariant()}-key", subjects);
        }
    }
}
