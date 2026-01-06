using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Battery;

public sealed class BatteryOptimizationService(
    ILogger logger,
    IGeneralSettingsService generalSettingsService,
    IBatteryOptimizationManager batteryOptimizationManager)
    : IBatteryOptimizationService, IDisposable
{
    private bool isDisposed;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public async Task MarkModalAsShownAsync()
    {
        try
        {
            const string Key = "AndroidBatteryOptimizationExclusionPromptShown";

            if (!await generalSettingsService.GeneralSettingExistsAsync(Key, cancellationTokenSource.Token))
            {
                await generalSettingsService.SetGeneralSettingAsync(Key, "True", cancellationTokenSource.Token);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error marking battery optimization modal as shown");
        }
    }

    public async Task<bool> ShouldShowModalAsync()
    {
        try
        {
            const string Key = "AndroidBatteryOptimizationExclusionPromptShown";
            // Check database to see if modal was already shown
            return !await generalSettingsService.GeneralSettingExistsAsync(Key, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking if battery optimization modal should be shown");
            return false;
        }
    }

    public void ShowOptimizationSettingsPage() => batteryOptimizationManager?.ShowBatteryOptimizationExclusionSettingsPage();

    public bool CanShowOptimizeActivity() => batteryOptimizationManager?.CanShowOptimizeActivity() ?? false;

    public void ShowDoNotDisturbSettingsPage() => batteryOptimizationManager?.ShowDoNotDisturbSettingsPage();

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

