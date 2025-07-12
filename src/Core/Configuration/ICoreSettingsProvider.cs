using System;

namespace Kafka.Ksql.Linq.Core.Configuration;
internal interface ICoreSettingsProvider
{
    CoreSettings GetSettings();
    void UpdateSettings(CoreSettings settings);
    event EventHandler<CoreSettingsChangedEventArgs>? SettingsChanged;
}
