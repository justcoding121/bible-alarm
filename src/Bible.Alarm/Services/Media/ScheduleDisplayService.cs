using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class ScheduleDisplayService(
    ILogger logger,
    IState<PlaybackState> playbackState,
    IAlarmScheduleService alarmScheduleService,
    IBibleSectionService bibleSectionService)
    : IScheduleDisplayService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<string> GetChapterDisplayNameAsync(int scheduleId, bool force = false) => await GetChapterDisplayNameForBibleReadingAsync(scheduleId, null, force);

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
                    if (!playbackState.Value.IsPreparingOrPlaying)
                    {
                        return string.Empty;
                    }
                }

                var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                    scheduleId, false, true, cancellationTokenSource.Token);

                if (schedule?.BibleReadingSchedule == null)
                {
                    return string.Empty;
                }

                scheduleToUse = schedule.BibleReadingSchedule;
            }

            if (scheduleToUse == null || !scheduleToUse.SectionNumber.HasValue)
            {
                return string.Empty;
            }

            var sectionName = await bibleSectionService.GetSectionNameAsync(
                scheduleToUse.LanguageCode,
                scheduleToUse.PublicationCode,
                scheduleToUse.SectionNumber.Value,
                cancellationTokenSource.Token);

            if (sectionName == null)
            {
                return string.Empty;
            }

            return $"{sectionName} {scheduleToUse.ChapterNumber}";
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while getting chapter display name for schedule {ScheduleId}", scheduleId);
            return string.Empty;
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

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

