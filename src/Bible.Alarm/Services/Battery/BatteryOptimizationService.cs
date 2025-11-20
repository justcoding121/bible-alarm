using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Database;
using Bible.Alarm.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Battery;

public class BatteryOptimizationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IBatteryOptimizationManager batteryOptimizationManager)
    : IBatteryOptimizationService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IBatteryOptimizationManager _batteryOptimizationManager = batteryOptimizationManager;

    public async Task MarkModalAsShownAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            
            if (!await scheduleDbContext.GeneralSettings.AnyAsync(x =>
                    x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
            {
                await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
                {
                    Key = "AndroidBatteryOptimizationExclusionPromptShown",
                    Value = "True"
                });

                await scheduleDbContext.SaveChangesAsync();
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
            using var scope = _scopeFactory.CreateScope();
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            return !await scheduleDbContext.GeneralSettings.AnyAsync(x =>
                x.Key == "AndroidBatteryOptimizationExclusionPromptShown");
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
}

