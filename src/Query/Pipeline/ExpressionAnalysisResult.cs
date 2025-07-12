using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
/// <summary>
/// 式解析結果（簡略版）
/// </summary>
internal class ExpressionAnalysisResult
{
    public List<MethodCallExpression> MethodCalls { get; set; } = new();
    public bool HasGroupBy => MethodCalls.Any(mc => mc.Method.Name == "GroupBy");
    public bool HasAggregation => MethodCalls.Any(mc => IsAggregateMethod(mc.Method.Name));
    public bool HasWindow => MethodCalls.Any(mc => mc.Method.Name == "Window");

    private static bool IsAggregateMethod(string methodName)
    {
        return methodName is "Sum" or "Count" or "Max" or "Min" or "Average" or "Aggregate";
    }
}
