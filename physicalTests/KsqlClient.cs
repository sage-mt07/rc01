using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
#nullable enable

namespace Kafka.Ksql.Linq.Tests.Integration;

public record KsqlResponse(bool IsSuccess, string Message, int? ErrorCode = null, string? ErrorDetail = null);

public interface IKsqlClient
{
    Task<KsqlResponse> ExecuteExplainAsync(string ksql);

    // ★これを追加
    Task<KsqlResponse> ExecuteStatementAsync(string statement);
}

public class KsqlClient : IKsqlClient
{
    private readonly HttpClient _httpClient;

    public KsqlClient(Uri baseUri)
    {
        _httpClient = new HttpClient { BaseAddress = baseUri };
    }

    public async Task<KsqlResponse> ExecuteExplainAsync(string ksql)
    {
        var payload = new
        {
            ksql = $"EXPLAIN {ksql}",
            streamsProperties = new { }
        };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/ksql", content);
        var body = await response.Content.ReadAsStringAsync();
        var success = response.IsSuccessStatusCode && !body.Contains("\"error_code\"");
        return new KsqlResponse(success, body);
    }

    // ★新規追加：任意のKSQL文実行
    public async Task<KsqlResponse> ExecuteStatementAsync(string statement)
    {
        var payload = new
        {
            ksql = statement,
            streamsProperties = new { }
        };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/ksql", content);
        var body = await response.Content.ReadAsStringAsync();
        var success = response.IsSuccessStatusCode && !body.Contains("\"error_code\"");
        return new KsqlResponse(success, body);
    }
}
