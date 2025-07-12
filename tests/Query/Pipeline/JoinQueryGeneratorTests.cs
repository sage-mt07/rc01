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
    public void GenerateThreeTableJoin_ReturnsJoinQuery()
    {
        Expression<Func<TestEntity, int>> k1 = e => e.Id;
        Expression<Func<ChildEntity, int>> k2 = c => c.ParentId;
        Expression<Func<ChildEntity, int>> k3 = c => c.Id;
        Expression<Func<GrandChildEntity, int>> k4 = g => g.ChildId;

        var generator = new JoinQueryGenerator();
        var sql = generator.GenerateThreeTableJoin(
            "TestEntity",
            "ChildEntity",
            "GrandChildEntity",
            k1,
            k2,
            k3,
            k4,
            resultSelector: null,
            isPullQuery: true);

        Assert.Contains("JOIN ChildEntity", sql);
        Assert.Contains("JOIN GrandChildEntity", sql);
        Assert.Contains("ON t1.Id = t2.ParentId", sql);
        Assert.Contains("ON t2.Id = t3.ChildId", sql);
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
