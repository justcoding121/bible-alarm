namespace Bible.Alarm.Common.Interfaces.Battery;

public interface IBatteryOptimizationService
{
    Task MarkModalAsShownAsync();
    Task<bool> ShouldShowModalAsync();
    void ShowOptimizationSettingsPage();
    bool CanShowOptimizeActivity();
}

