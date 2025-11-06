namespace Bible.Alarm.Common.Interfaces.Battery;

public interface IBatteryOptimizationManager : IDisposable
{
    void ShowBatteryOptimizationExclusionSettingsPage();
    bool CanShowOptimizeActivity();
}