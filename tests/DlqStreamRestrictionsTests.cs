using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests;

public class DlqStreamRestrictionsTests
{
    private class TestContext : KsqlContext
    {
        public TestContext() : base(new KsqlDslOptions()) { }
        protected override bool SkipSchemaRegistration => true;
    }

    [Fact]
    public async Task DlqStream_ForEachAsync_Allows()
    {
        var ctx = new TestContext();
        var stream = ctx.Set<DlqEnvelope>();
        await stream.ForEachAsync(_ => Task.CompletedTask);
    }

    [Fact]
    public async Task DlqStream_ToListAsync_Throws()
    {
        var ctx = new TestContext();
        var stream = ctx.Set<DlqEnvelope>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => stream.ToListAsync());
        Assert.Contains("DLQ", ex.Message);
    }

}
