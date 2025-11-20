namespace Bible.Alarm.Services.Battery.Interfaces;

public interface IBatteryOptimizationService
{
    Task MarkModalAsShownAsync();
    Task<bool> ShouldShowModalAsync();
    void ShowOptimizationSettingsPage();
    bool CanShowOptimizeActivity();
}

