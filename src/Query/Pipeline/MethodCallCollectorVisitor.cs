using System.Collections.Generic;
using System.Linq.Expressions;
internal class MethodCallCollectorVisitor : ExpressionVisitor
{
    public List<MethodCallExpression> MethodCalls { get; } = new();

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        MethodCalls.Add(node);
        return base.VisitMethodCall(node);
    }
}
