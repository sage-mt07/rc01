using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Builders;
/// <summary>
/// Helper for processing LINQ Window method calls into KSQL WINDOW clauses.
/// </summary>
internal static class WindowExpressionVisitorExtensions
{
    internal static string ProcessWindowOperation(MethodCallExpression methodCall)
    {
        if (methodCall == null)
            throw new ArgumentNullException(nameof(methodCall));

        if (methodCall.Method.Name != "Window")
            throw new InvalidOperationException("Expression is not a Window call");

        if (methodCall.Arguments[0] is MethodCallExpression inner &&
            inner.Method.Name == "Window")
        {
            throw new InvalidOperationException("Multiple Window calls are not supported");
        }

        if (methodCall.Arguments.Count < 2)
            throw new InvalidOperationException("Window call missing definition");

        var argument = methodCall.Arguments[1];

        // evaluate the argument expression so WindowClauseBuilder receives
        // a simple ConstantExpression of WindowDef or TimeSpan
        object? value = Expression.Lambda(argument).Compile().DynamicInvoke();
        Expression constExpr = value switch
        {
            WindowDef def => Expression.Constant(def, typeof(WindowDef)),
            TimeSpan ts => Expression.Constant(ts, typeof(TimeSpan)),
            _ => argument
        };

        var builder = new WindowClauseBuilder();

        try
        {
            var content = builder.Build(constExpr);
            return $"WINDOW {content}";
        }
        catch (InvalidOperationException ex)
        {
            throw new NotSupportedException(ex.Message, ex);
        }
    }
}
