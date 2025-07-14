using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Query.Schema;

/// <summary>
/// クエリスキーマ解析結果
/// </summary>
public class QuerySchemaResult
{
    public QuerySchema? Schema { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();

    public static QuerySchemaResult Failure(string error) =>
        new() { Success = false, ErrorMessage = error };

    public static QuerySchemaResult CreateSuccess(QuerySchema schema) =>
        new() { Success = true, Schema = schema };
}
