using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Kafka.Ksql.Linq.Query.Pipeline;
using Kafka.Ksql.Linq.Tests;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Query.Pipeline;

public class JoinQueryGeneratorTests
{
    [Fact]
    public void GenerateTwoTableJoin_ReturnsJoinQuery()
    {
        Expression<Func<TestEntity, int>> outerKey = e => e.Id;
        Expression<Func<ChildEntity, int>> innerKey = c => c.ParentId;

        var generator = new JoinQueryGenerator();
        var sql = generator.GenerateTwoTableJoin(
            "TestEntity",
            "ChildEntity",
            outerKey,
            innerKey,
            resultSelector: null,
            whereCondition: null,
            isPullQuery: true);

        Assert.Contains("FROM TestEntity", sql);
        Assert.Contains("JOIN ChildEntity", sql);
        Assert.Contains("ON e.Id = c.ParentId", sql);
    }


    [Fact]
    public void GenerateLeftJoin_ReturnsLeftJoinQuery()
    {
        Expression<Func<TestEntity, int>> outerKey = e => e.Id;
        Expression<Func<ChildEntity, int>> innerKey = c => c.ParentId;

        var generator = new JoinQueryGenerator();
        var sql = generator.GenerateLeftJoin(
            "TestEntity",
            "ChildEntity",
            outerKey,
            innerKey,
            resultSelector: null,
            isPullQuery: true);

        Assert.Contains("LEFT JOIN ChildEntity", sql);
        Assert.DoesNotContain("EMIT CHANGES", sql);
    }
}
