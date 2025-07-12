using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq;

public class WindowDef
{
    internal readonly List<(string Name, object? Value)> Operations = new();

    public WindowDef TumblingWindow()
    {
        Operations.Add((nameof(TumblingWindow), null));
        return this;
    }

    public WindowDef HoppingWindow()
    {
        Operations.Add((nameof(HoppingWindow), null));
        return this;
    }

    public WindowDef SessionWindow()
    {
        Operations.Add((nameof(SessionWindow), null));
        return this;
    }

    public WindowDef Size(TimeSpan ts)
    {
        Operations.Add((nameof(Size), ts));
        return this;
    }

    public WindowDef AdvanceBy(TimeSpan ts)
    {
        Operations.Add((nameof(AdvanceBy), ts));
        return this;
    }

    public WindowDef Gap(TimeSpan ts)
    {
        Operations.Add((nameof(Gap), ts));
        return this;
    }

    public WindowDef Retention(TimeSpan ts)
    {
        Operations.Add((nameof(Retention), ts));
        return this;
    }

    public WindowDef GracePeriod(TimeSpan ts)
    {
        Operations.Add((nameof(GracePeriod), ts));
        return this;
    }

    public WindowDef EmitFinal()
    {
        Operations.Add((nameof(EmitFinal), null));
        return this;
    }
}
