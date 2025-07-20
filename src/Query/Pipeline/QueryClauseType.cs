namespace Kafka.Ksql.Linq.Query.Pipeline;

/// <summary>
/// クエリ句タイプ列挙
/// </summary>
internal enum QueryClauseType
{
    Select,
    From,
    Join,
    Where,
    GroupBy,
    Window,
    Having,
    OrderBy,
    Limit,
    EmitChanges
}

