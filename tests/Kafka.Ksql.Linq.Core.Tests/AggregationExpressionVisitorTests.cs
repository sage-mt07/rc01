using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Kafka.Ksql.Linq.Core.Window;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Tests;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

namespace Kafka.Ksql.Linq.Core.Tests;

public class AggregationExpressionVisitorTests
{
    [Fact]
    public void GenerateAggregationFunction_SumAndCount()
    {
        var visitor = Activator.CreateInstance(typeof(AggregationExpressionVisitor), true)!;
        var param = Expression.Parameter(typeof(IGrouping<int, TestEntity>), "g");
        var lambda = Expression.Lambda(Expression.Property(Expression.Parameter(typeof(TestEntity), "x"), nameof(TestEntity.Id)), Expression.Parameter(typeof(TestEntity), "x"));
        var sumMethod = typeof(Enumerable).GetMethods().First(m => m.Name == "Sum" && m.GetParameters().Length == 2).MakeGenericMethod(typeof(TestEntity));
        var sumCall = Expression.Call(sumMethod, param, lambda);
        var result = InvokePrivate<string>(visitor, "GenerateAggregationFunction", new[] { typeof(MethodCallExpression) }, null, sumCall);
        Assert.Equal("SUM(*)", result);

        var countMethod = typeof(Enumerable).GetMethods().First(m => m.Name == "Count" && m.GetParameters().Length == 1).MakeGenericMethod(typeof(TestEntity));
        var countCall = Expression.Call(countMethod, param);
        result = InvokePrivate<string>(visitor, "GenerateAggregationFunction", new[] { typeof(MethodCallExpression) }, null, countCall);
        Assert.Equal("COUNT(*)", result);
    }

    [Fact]
    public void GenerateAggregationFunction_OffsetAndUnknown()
    {
        var visitor = Activator.CreateInstance(typeof(AggregationExpressionVisitor), true)!;
        var param = Expression.Parameter(typeof(IGrouping<int, TestEntity>), "g");
        var lambda = Expression.Lambda(Expression.Property(Expression.Parameter(typeof(TestEntity), "x"), nameof(TestEntity.Id)), Expression.Parameter(typeof(TestEntity), "x"));
        var latestMethod = typeof(OffsetAggregateExtensions).GetMethod("LatestByOffset")!.MakeGenericMethod(typeof(TestEntity), typeof(int), typeof(int));
        var latestCall = Expression.Call(latestMethod, param, lambda);
        var result = InvokePrivate<string>(visitor, "GenerateAggregationFunction", new[] { typeof(MethodCallExpression) }, null, latestCall);
        Assert.Equal("LATEST_BY_OFFSET(*)", result);

        var customMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!;
        var customCall = Expression.Call(Expression.Constant("test"), customMethod, Expression.Constant("a"));
        result = InvokePrivate<string>(visitor, "GenerateAggregationFunction", new[] { typeof(MethodCallExpression) }, null, customCall);
        Assert.Equal("STARTSWITH(UNKNOWN)", result);
    }
}
