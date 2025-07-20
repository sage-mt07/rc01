using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Kafka.Ksql.Linq.Application;

public record KsqlDbResponse(bool IsSuccess, string Message, int? ErrorCode = null, string? ErrorDetail = null);

public static class KsqlDbExecutionExtensions
{
    private static readonly Uri DefaultKsqlDbUrl = new("http://localhost:8088");

    private static CommonSection GetCommonSection(KsqlContext context)
    {
        var options = context.GetOptions();
        var config = options.Configuration;

        var section = new CommonSection { BootstrapServers = options.BootstrapServers };
        config?.GetSection("KsqlDsl:Common").Bind(section);
        return section;
    }

    private static Uri GetKsqlDbUrl(KsqlContext context)
    {
        var config = context.GetOptions().Configuration;

        var schemaUrl = config?["KsqlDsl:SchemaRegistry:Url"];
        if (!string.IsNullOrWhiteSpace(schemaUrl) &&
            Uri.TryCreate(schemaUrl, UriKind.Absolute, out var uri))
        {
            var port = uri.IsDefaultPort ? DefaultKsqlDbUrl.Port : uri.Port;
            return new Uri($"{uri.Scheme}://{uri.Host}:{port}");
        }

        var bootstrap = GetCommonSection(context).BootstrapServers;
        if (!string.IsNullOrWhiteSpace(bootstrap))
        {
            var first = bootstrap.Split(',')[0];
            var hostParts = first.Split(':');
            var host = hostParts[0];
            int port = DefaultKsqlDbUrl.Port;
            if (hostParts.Length > 1 && int.TryParse(hostParts[1], out var parsed))
            {
                port = parsed;
            }
            return new Uri($"http://{host}:{port}");
        }

        return DefaultKsqlDbUrl;
    }

    private static HttpClient CreateClient(KsqlContext context)
    {
        return new HttpClient { BaseAddress = GetKsqlDbUrl(context) };
    }

    public static async Task<KsqlDbResponse> ExecuteStatementAsync(this KsqlContext context, string statement)
    {
        using var client = CreateClient(context);
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
