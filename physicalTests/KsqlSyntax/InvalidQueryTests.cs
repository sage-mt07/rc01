using Kafka.Ksql.Linq.Application;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;


public class InvalidQueryTests
{
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("SELECT ID, COUNT(*) FROM ORDERS GROUP BY ID;")]
    [InlineData("SELECT CASE WHEN ID=1 THEN 'A' ELSE 2 END FROM ORDERS EMIT CHANGES;")]
    public async Task GeneratedQuery_IsRejected(string ksql)
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);
        try
        {
            await TestEnvironment.ResetAsync();

        }
        catch (System.Exception)
        {

        }
 
        await using var ctx = TestEnvironment.CreateContext();
        var response = await ctx.ExecuteExplainAsync(ksql);
        Assert.False(response.IsSuccess, $"{ksql} unexpectedly succeeded");
    }
}
