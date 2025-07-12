using System;

namespace Kafka.Ksql.Linq.Core.Configuration;
internal class CoreSettingsChangedEventArgs : EventArgs
{
    public CoreSettings OldSettings { get; }
    public CoreSettings NewSettings { get; }
    public DateTime ChangedAt { get; }

    public CoreSettingsChangedEventArgs(CoreSettings oldSettings, CoreSettings newSettings)
    {
        OldSettings = oldSettings;
        NewSettings = newSettings;
        ChangedAt = DateTime.UtcNow;
    }
}
