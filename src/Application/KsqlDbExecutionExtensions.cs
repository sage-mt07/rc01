using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Application;

public record KsqlDbResponse(bool IsSuccess, string Message, int? ErrorCode = null, string? ErrorDetail = null);

public static class KsqlDbExecutionExtensions
{
    private static readonly Uri DefaultKsqlDbUrl = new("http://localhost:8088");

    private static HttpClient CreateClient()
    {
        return new HttpClient { BaseAddress = DefaultKsqlDbUrl };
    }

    public static async Task<KsqlDbResponse> ExecuteStatementAsync(this KsqlContext context, string statement)
    {
        using var client = CreateClient();
        var payload = new { ksql = statement, streamsProperties = new { } };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/ksql", content);
        var body = await response.Content.ReadAsStringAsync();
        var success = response.IsSuccessStatusCode && !body.Contains("\"error_code\"");
        return new KsqlDbResponse(success, body);
    }

    public static Task<KsqlDbResponse> ExecuteExplainAsync(this KsqlContext context, string ksql)
    {
        return context.ExecuteStatementAsync($"EXPLAIN {ksql}");
    }
}
