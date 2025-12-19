using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Database;

public class DatabaseSeedService(
    ILogger logger,
    IAlarmScheduleService alarmScheduleService,
    IBibleTranslationService bibleTranslationService,
    IMelodyMusicService melodyMusicService)
    : IDatabaseSeedService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IAlarmScheduleService _alarmScheduleService = alarmScheduleService;
    private readonly IBibleTranslationService _bibleTranslationService = bibleTranslationService;
    private readonly IMelodyMusicService _melodyMusicService = melodyMusicService;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    public async Task SeedDefaultAlarmAsync()
    {
        // Seed if schedules are empty
        if (!await _alarmScheduleService.AnySchedulesExistAsync(_cancellationTokenSource.Token))
        {
            // Create sample schedule with IsEnabled = false (disabled by default)
            var schedule = await AlarmSchedule.GetSampleSchedule(false, _bibleTranslationService, _melodyMusicService);

            await _alarmScheduleService.AddScheduleAsync(schedule, _cancellationTokenSource.Token);

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

