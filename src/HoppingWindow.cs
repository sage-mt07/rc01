using System;

namespace Kafka.Ksql.Linq;

public static class HoppingWindow
{
    public static WindowDef Of(TimeSpan size) => new WindowDef().HoppingWindow().Size(size);
    public static WindowDef OfMinutes(int minutes) => Of(TimeSpan.FromMinutes(minutes));
}
