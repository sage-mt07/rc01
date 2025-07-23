using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class SchemaRegistryResetTests
{
    private static readonly HttpClient Http = new();

    // Reset 後に全テーブルのスキーマが登録されているか確認
    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task Setup_ShouldRegisterAllSchemas()
    {
        await TestEnvironment.ResetAsync();

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
    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task DuplicateSchemaRegistration_ShouldSucceed()
    {
        await TestEnvironment.ResetAsync();

        var latest = await Http.GetFromJsonAsync<JsonElement>($"{TestEnvironment.SchemaRegistryUrl}/subjects/orders-value/versions/latest");
        var schema = latest.GetProperty("schema").GetString();
        var resp = await Http.PostAsJsonAsync($"{TestEnvironment.SchemaRegistryUrl}/subjects/orders-value/versions", new { schema });
        resp.EnsureSuccessStatusCode();
    }

    // 大文字のサブジェクト名が存在しないことを確認
    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task UpperCaseSubjects_ShouldNotExist()
    {
        await TestEnvironment.ResetAsync();
        var subjects = await Http.GetFromJsonAsync<string[]>($"{TestEnvironment.SchemaRegistryUrl}/subjects");
        Assert.NotNull(subjects);

        foreach (var table in TestSchema.AllTopicNames)
        {
            Assert.DoesNotContain($"{table.ToUpperInvariant()}-value", subjects);
            Assert.DoesNotContain($"{table.ToUpperInvariant()}-key", subjects);
        }
    }
}
