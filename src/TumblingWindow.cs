using System;

namespace Kafka.Ksql.Linq;

public static class TumblingWindow
{
    public static WindowDef Of(TimeSpan size) => new WindowDef().TumblingWindow().Size(size);
    public static WindowDef OfMinutes(int minutes) => Of(TimeSpan.FromMinutes(minutes));
}
