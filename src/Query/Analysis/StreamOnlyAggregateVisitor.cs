using System.Linq.Expressions;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Query.Analysis;

/// <summary>
/// Visitor to detect stream-only aggregate functions like MIN/MAX.
/// </summary>
internal class StreamOnlyAggregateVisitor : ExpressionVisitor
{
    public bool HasStreamOnlyAggregate { get; private set; }

    private static readonly HashSet<string> StreamOnlyFunctions = new(
        new[] { "Min", "Max" });

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var name = node.Method.Name;
        if (StreamOnlyFunctions.Contains(name))
        {
            HasStreamOnlyAggregate = true;
        }

        return base.VisitMethodCall(node);
    }
}
