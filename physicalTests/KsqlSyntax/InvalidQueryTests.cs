using Kafka.Ksql.Linq.Application;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class InvalidQueryTests
{
    [KsqlDbTheory]
    [Trait("Category", "Integration")]
    [InlineData("SELECT ID, COUNT(*) FROM ORDERS GROUP BY ID;")]
    [InlineData("SELECT CASE WHEN ID=1 THEN 'A' ELSE 2 END FROM ORDERS EMIT CHANGES;")]
    public async Task GeneratedQuery_IsRejected(string ksql)
    {
        await TestEnvironment.ResetAsync();

        await using var ctx = TestEnvironment.CreateContext();
        var response = await ctx.ExecuteExplainAsync(ksql);
        Assert.False(response.IsSuccess, $"{ksql} unexpectedly succeeded");
    }
}
