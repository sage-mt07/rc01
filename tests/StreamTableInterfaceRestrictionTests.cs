using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Query.Abstractions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests;

public class StreamTableInterfaceRestrictionTests
{
    private class TestContext : KsqlContext
    {
        public TestContext() : base(new KsqlDslOptions()) { }
        protected override bool SkipSchemaRegistration => true;
    }

    private static EntityModel CreateTableModel()
    {
        var model = new EntityModel
        {
            EntityType = typeof(TestEntity),
            TopicName = "t",
            AllProperties = typeof(TestEntity).GetProperties(),
            KeyProperties = new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Id))! }
        };
        model.SetStreamTableType(StreamTableType.Table);
        return model;
    }

    private static EntityModel CreateStreamModel()
    {
        var model = new EntityModel
        {
            EntityType = typeof(TestEntity),
            TopicName = "s",
            AllProperties = typeof(TestEntity).GetProperties(),
            KeyProperties = Array.Empty<PropertyInfo>()
        };
        model.SetStreamTableType(StreamTableType.Stream);
        return model;
    }

    [Fact]
    public async Task Table_ForEachAsync_Throws()
    {
        var ctx = new TestContext();
        var set = new EventSetWithServices<TestEntity>(ctx, CreateTableModel());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => set.ForEachAsync(_ => Task.CompletedTask));
        Assert.Contains("ForEachAsync() is not supported", ex.Message);
    }

    [Fact]
    public async Task Stream_ToListAsync_Throws()
    {
        var ctx = new TestContext();
        var set = new EventSetWithServices<TestEntity>(ctx, CreateStreamModel());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => set.ToListAsync());
        Assert.Contains("ToListAsync() is not supported", ex.Message);
    }

    [Fact]
    public async Task Table_ToListAsync_Allows()
    {
        var ctx = new TestContext();
        var set = new EventSetWithServices<TestEntity>(ctx, CreateTableModel());
        var list = await set.ToListAsync();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task Stream_ForEachAsync_Allows()
    {
        var ctx = new TestContext();
        var set = new EventSetWithServices<TestEntity>(ctx, CreateStreamModel());
        await set.ForEachAsync(_ => Task.CompletedTask);
    }
}
