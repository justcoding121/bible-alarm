using System;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Scheduler;

public class ScheduleSelectionService(
    ILogger logger,
    IAlarmMusicService alarmMusicService,
    IBibleReadingScheduleService bibleReadingScheduleService)
    : IScheduleSelectionService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IAlarmMusicService _alarmMusicService = alarmMusicService;
    private readonly IBibleReadingScheduleService _bibleReadingScheduleService = bibleReadingScheduleService;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<AlarmMusic> LoadMusicForSelectionAsync(int scheduleId, bool isNewSchedule, bool musicUpdated, AlarmMusic currentMusic)
    {
        try
        {
            // Get the latest music track if needed
            if (currentMusic == null || (!isNewSchedule && !musicUpdated))
            {
                var music = await _alarmMusicService.GetMusicByScheduleIdAsync(scheduleId, _cancellationTokenSource.Token);
                if (music == null)
                    throw new InvalidOperationException($"Music not found for schedule {scheduleId}");
                return music;
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
                var bibleReading = await _bibleReadingScheduleService.GetBibleReadingScheduleByScheduleIdAsync(scheduleId, _cancellationTokenSource.Token);
                if (bibleReading == null)
                    throw new InvalidOperationException($"BibleReadingSchedule not found for schedule {scheduleId}");
                return bibleReading;
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

