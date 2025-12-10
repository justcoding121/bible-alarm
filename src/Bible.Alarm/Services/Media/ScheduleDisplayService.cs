using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class ScheduleDisplayService(
    ILogger logger,
    IState<PlaybackState> playbackState,
    IAlarmScheduleService alarmScheduleService,
    IBibleBookService bibleBookService)
    : IScheduleDisplayService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IState<PlaybackState> _playbackState = playbackState;
    private readonly IAlarmScheduleService _alarmScheduleService = alarmScheduleService;
    private readonly IBibleBookService _bibleBookService = bibleBookService;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task<string> GetChapterDisplayNameAsync(int scheduleId, bool force = false)
    {
        return await GetChapterDisplayNameForBibleReadingAsync(scheduleId, null, force);
    }

    public async Task<string> GetChapterDisplayNameForBibleReadingAsync(int scheduleId, BibleReadingSchedule bibleReadingSchedule, bool force = false)
    {
        try
        {
            var scheduleToUse = bibleReadingSchedule;

            // If bibleReadingSchedule is not provided, load it from database
            if (scheduleToUse == null)
            {
                if (!force)
                {
                    if (!_playbackState.Value.IsPreparingOrPlaying) return string.Empty;
                }

                var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
                    scheduleId, false, true, _cancellationTokenSource.Token);

                if (schedule?.BibleReadingSchedule == null) return string.Empty;
                scheduleToUse = schedule.BibleReadingSchedule;
            }

            if (scheduleToUse == null) return string.Empty;

            var bookName = await _bibleBookService.GetBookNameAsync(
                scheduleToUse.LanguageCode,
                scheduleToUse.PublicationCode,
                scheduleToUse.BookNumber,
                _cancellationTokenSource.Token);

            if (bookName == null) return string.Empty;

            return $"{bookName} {scheduleToUse.ChapterNumber}";
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened while getting chapter display name for schedule {ScheduleId}", scheduleId);
            return string.Empty;
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
        
        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

