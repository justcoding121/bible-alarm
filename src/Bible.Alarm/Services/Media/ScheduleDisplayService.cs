using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
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
    IBiblePublicationSectionService biblePublicationSectionService)
    : IScheduleDisplayService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<string> GetTrackDisplayNameAsync(int scheduleId, bool force = false) => await GetTrackDisplayNameForBiblePublicationAsync(scheduleId, null, force);

    public async Task<string> GetTrackDisplayNameForBiblePublicationAsync(int scheduleId, BiblePublicationSchedule biblePublicationSchedule, bool force = false)
    {
        try
        {
            var scheduleToUse = biblePublicationSchedule;

            // If biblePublicationSchedule is not provided, load it from database
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

                if (schedule?.BiblePublicationSchedule == null)
                {
                    return string.Empty;
                }

                scheduleToUse = schedule.BiblePublicationSchedule;
            }

            if (scheduleToUse == null || !scheduleToUse.SectionNumber.HasValue)
            {
                return string.Empty;
            }

            var sectionName = await biblePublicationSectionService.GetSectionNameAsync(
                scheduleToUse.LanguageCode,
                scheduleToUse.PublicationCode,
                scheduleToUse.SectionNumber.Value,
                cancellationTokenSource.Token);

            if (sectionName == null)
            {
                return string.Empty;
            }

            return $"{sectionName} {scheduleToUse.TrackNumber}";
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened while getting track display name for schedule {ScheduleId}", scheduleId);
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

