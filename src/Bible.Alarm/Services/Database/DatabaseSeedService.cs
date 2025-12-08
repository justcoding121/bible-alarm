using Bible.Alarm.Shared.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Database;

public class DatabaseSeedService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IDatabaseSeedService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task SeedDefaultAlarmAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        var mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        if (!await scheduleDbContext.AlarmSchedules.AnyAsync(_cancellationTokenSource.Token)
            && !await scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == AppConstants.GeneralSettingsKeys.AlarmSeeded, _cancellationTokenSource.Token)
            //for existing apps before version 1.30
            && !await scheduleDbContext.GeneralSettings.AnyAsync(x =>
                x.Key == AppConstants.GeneralSettingsKeys.AndroidBatteryOptimizationExclusionPromptShown, _cancellationTokenSource.Token))
        {
            var schedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);

            await scheduleDbContext.AlarmSchedules.AddAsync(schedule, _cancellationTokenSource.Token);
            await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
            {
                Key = AppConstants.GeneralSettingsKeys.AlarmSeeded,
                Value = "True"
            }, _cancellationTokenSource.Token);

            await scheduleDbContext.SaveChangesAsync(_cancellationTokenSource.Token);
        }
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
        
        // IServiceScopeFactory is a singleton, so don't dispose it
        // No event handlers to unsubscribe
    }
}

