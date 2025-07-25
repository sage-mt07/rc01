using Kafka.Ksql.Linq.Core.Modeling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Query.Pipeline;
using Xunit;
using System.IO;
namespace Kafka.Ksql.Linq.Tests.Query.Pipeline;

public class DDLQueryGeneratorTests
{
    private static T ExecuteInScope<T>(Func<T> func)
    {
        using (ModelCreatingScope.Enter())
        {
            return func();
        }
    }
    private static EntityModel CreateEntityModel()
    {
        return new EntityModel
        {
            EntityType = typeof(TestEntity),
            KeyProperties = new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Id))! },
            AllProperties = typeof(TestEntity).GetProperties()
        };
    }

    [Fact]
    public void GenerateCreateStream_CreatesExpectedStatement()
    {
        var model = CreateEntityModel();
        var generator = new DDLQueryGenerator();
        var query = ExecuteInScope(() => generator.GenerateCreateStream("s1", "topic", model));
        Assert.Contains("CREATE STREAM s1", query);
        Assert.Contains("KAFKA_TOPIC='topic'", query);
        File.AppendAllText("generated_queries.txt", query + Environment.NewLine);
    }

    [Fact]
    public void GenerateCreateStream_UsesPartitionFromAttribute()
    {
        var builder = new ModelBuilder();
        builder.Entity<TestEntity>()
            .WithTopic("topic");
        var model = builder.GetEntityModel<TestEntity>()!;
        var generator = new DDLQueryGenerator();
        var query = ExecuteInScope(() => generator.GenerateCreateStream("s1", "topic", model));
        Assert.Contains("CREATE STREAM", query);
    }

    [Fact]
    public void GenerateCreateStream_IncludesKeyFormat()
    {
        var model = CreateEntityModel();
        var generator = new DDLQueryGenerator();
        var query = ExecuteInScope(() => generator.GenerateCreateStream("s1", "topic", model));
        Assert.Contains("KEY_FORMAT='AVRO'", query);
    }

    [Fact]
    public void GenerateCreateTable_IncludesKeyFormat()
    {
        var model = CreateEntityModel();
        var generator = new DDLQueryGenerator();
        var query = ExecuteInScope(() => generator.GenerateCreateTable("t1", "topic", model));
        Assert.Contains("KEY_FORMAT='AVRO'", query);
    }

    [Fact]
    public void GenerateCreateTableAs_WithWhereAndGroupBy()
    {
        IQueryable<TestEntity> source = new List<TestEntity>().AsQueryable();
        var expr = source.Where(e => e.IsActive)
                         .GroupBy(e => e.Type)
                         .Select(g => new { g.Key, Count = g.Count() });
        var generator = new DDLQueryGenerator();
        var query = ExecuteInScope(() => generator.GenerateCreateTableAs("t1", "Base", expr.Expression));
        Assert.Contains("CREATE TABLE t1 AS SELECT", query);
        Assert.Contains("FROM Base", query);
        Assert.Contains("WHERE (IsActive = true)", query);
        Assert.Contains("GROUP BY Type", query);
        File.AppendAllText("generated_queries.txt", query + Environment.NewLine);
    }

    [Fact]
    public void GenerateCreateStream_OutsideScope_Throws()
    {
        var model = CreateEntityModel();
        var generator = new DDLQueryGenerator();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            generator.GenerateCreateStream("s1", "topic", model));

        Assert.Contains("Where/GroupBy/Select", ex.Message);
    }
}
