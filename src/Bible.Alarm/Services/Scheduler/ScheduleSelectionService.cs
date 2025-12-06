using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

public class ScheduleSelectionService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IScheduleSelectionService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<AlarmMusic> LoadMusicForSelectionAsync(int scheduleId, bool isNewSchedule, bool musicUpdated, AlarmMusic currentMusic)
    {
        try
        {
            // Get the latest music track if needed
            if (currentMusic == null || (!isNewSchedule && !musicUpdated))
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                return await scheduleDbContext.AlarmMusic
                    .AsNoTracking()
                    .FirstAsync(x => x.AlarmScheduleId == scheduleId, _cancellationTokenSource.Token);
            }

            return currentMusic;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading music for selection for schedule {ScheduleId}", scheduleId);
            return currentMusic;
        }
    }

    public async Task<BibleReadingSchedule> LoadBibleReadingForSelectionAsync(int scheduleId, bool isNewSchedule, bool bibleReadingUpdated, BibleReadingSchedule currentBibleReading)
    {
        try
        {
            // Get the latest bible track if needed
            if (currentBibleReading == null || (!isNewSchedule && !bibleReadingUpdated))
            {
                using var scope = _scopeFactory.CreateScope();
                var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
                return await scheduleDbContext.BibleReadingSchedules
                    .AsNoTracking()
                    .FirstAsync(x => x.AlarmScheduleId == scheduleId, _cancellationTokenSource.Token);
            }

            return currentBibleReading;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading bible reading for selection for schedule {ScheduleId}", scheduleId);
            return currentBibleReading;
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

