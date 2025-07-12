using System;

namespace Kafka.Ksql.Linq;

public static class SessionWindow
{
    public static WindowDef Of(TimeSpan gap) => new WindowDef().SessionWindow().Gap(gap);
    public static WindowDef OfMinutes(int minutes) => Of(TimeSpan.FromMinutes(minutes));
}
