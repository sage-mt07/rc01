using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Query.Analysis;


/// <summary>
/// クエリ解析用Visitor
/// </summary>
internal class QueryAnalysisVisitor : ExpressionVisitor
{
    public Expression? GroupByExpression { get; private set; }
    public Expression? SelectExpression { get; private set; }
    public bool HasWindow { get; private set; }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case "GroupBy":
                if (node.Arguments.Count >= 2)
                    GroupByExpression = node.Arguments[1];
                break;

            case "Select":
                if (node.Arguments.Count >= 2)
                    SelectExpression = node.Arguments[1];
                break;

            case "Window":
                HasWindow = true;
                break;
        }

        return base.VisitMethodCall(node);
    }
}
