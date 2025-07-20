using System;
using Xunit;
using Kafka.Ksql.Linq.Application;

namespace Kafka.Ksql.Linq.Tests.Integration;

public static class KsqlDbAvailability
{
    public const string SkipReason = "Skipped in CI due to missing ksqlDB instance or schema setup failure";
    private static bool _checked;
    private static bool _available;

    public static bool IsAvailable()
    {
        if (_checked)
            return _available;

        try
        {
            TestEnvironment.ResetAsync().GetAwaiter().GetResult();
            using var ctx = TestEnvironment.CreateContext();
            var r = ctx.ExecuteStatementAsync("SHOW TOPICS;").GetAwaiter().GetResult();
            _available = r.IsSuccess;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test setup failed: {ex.Message}");
            _available = false;
        }

        _checked = true;
        return _available;
    }
}

public class KsqlDbFactAttribute : FactAttribute
{
    public KsqlDbFactAttribute()
    {
        if (!KsqlDbAvailability.IsAvailable())
        {
            Skip = KsqlDbAvailability.SkipReason;
        }
    }
}

public class KsqlDbTheoryAttribute : TheoryAttribute
{
    public KsqlDbTheoryAttribute()
    {
        if (!KsqlDbAvailability.IsAvailable())
        {
            Skip = KsqlDbAvailability.SkipReason;
        }
    }
}
