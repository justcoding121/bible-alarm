using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Battery;

public class BatteryOptimizationService(
    ILogger logger,
    IGeneralSettingsService generalSettingsService,
    IBatteryOptimizationManager batteryOptimizationManager)
    : IBatteryOptimizationService, IDisposable
{
    private bool _isDisposed;
    private readonly ILogger _logger = logger;
    private readonly IGeneralSettingsService _generalSettingsService = generalSettingsService;
    private readonly IBatteryOptimizationManager _batteryOptimizationManager = batteryOptimizationManager;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    public async Task MarkModalAsShownAsync()
    {
        try
        {
            const string key = "AndroidBatteryOptimizationExclusionPromptShown";
            
            if (!await _generalSettingsService.GeneralSettingExistsAsync(key, _cancellationTokenSource.Token))
            {
                await _generalSettingsService.SetGeneralSettingAsync(key, "True", _cancellationTokenSource.Token);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking battery optimization modal as shown");
        }
    }

    public async Task<bool> ShouldShowModalAsync()
    {
        try
        {
            const string key = "AndroidBatteryOptimizationExclusionPromptShown";
            return !await _generalSettingsService.GeneralSettingExistsAsync(key, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking if battery optimization modal should be shown");
            return false;
        }
    }

    public void ShowOptimizationSettingsPage()
    {
        _batteryOptimizationManager?.ShowBatteryOptimizationExclusionSettingsPage();
    }

    public bool CanShowOptimizeActivity()
    {
        return _batteryOptimizationManager?.CanShowOptimizeActivity() ?? false;
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Cancel and dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            _logger.Warning(ex, "Error during cancellation token source disposal");
        }
        
        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

