using System;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

internal static class KsqlDbAvailability
{
    private static readonly IKsqlClient _client = new KsqlClient(new Uri("http://localhost:8088"));
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
            var r = _client.ExecuteStatementAsync("SHOW TOPICS;").GetAwaiter().GetResult();
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
