using Bible.Alarm.Models;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
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

        // Seed if schedules are empty
        if (!await scheduleDbContext.AlarmSchedules.AnyAsync(_cancellationTokenSource.Token))
        {
            // Create sample schedule with IsEnabled = false (disabled by default)
            var schedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);

            await scheduleDbContext.AlarmSchedules.AddAsync(schedule, _cancellationTokenSource.Token);
            await scheduleDbContext.SaveChangesAsync(_cancellationTokenSource.Token);
            
            _logger.Information("Seeded default alarm schedule. ScheduleId={ScheduleId}, Name={Name}", 
                schedule.Id, schedule.Name);
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

