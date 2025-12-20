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
    private readonly ILogger logger = logger;
    private readonly IAlarmScheduleService alarmScheduleService = alarmScheduleService;
    private readonly IBibleTranslationService bibleTranslationService = bibleTranslationService;
    private readonly IMelodyMusicService melodyMusicService = melodyMusicService;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task SeedDefaultAlarmAsync()
    {
        // Seed if schedules are empty
        if (!await alarmScheduleService.AnySchedulesExistAsync(cancellationTokenSource.Token))
        {
            // Create sample schedule with IsEnabled = false (disabled by default)
            var schedule = await AlarmSchedule.GetSampleSchedule(false, bibleTranslationService, melodyMusicService);

            await alarmScheduleService.AddScheduleAsync(schedule, cancellationTokenSource.Token);

            logger.Information("Seeded default alarm schedule. ScheduleId={ScheduleId}, Name={Name}",
                schedule.Id, schedule.Name);
        }
    }

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

        // IServiceScopeFactory is a singleton, so don't dispose it
        // No event handlers to unsubscribe
    }
}

