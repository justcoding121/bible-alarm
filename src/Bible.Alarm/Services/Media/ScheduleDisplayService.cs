#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
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
    IBiblePublicationSectionService biblePublicationSectionService,
    IBiblePublicationService biblePublicationService)
    : IScheduleDisplayService, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<string> GetTrackDisplayNameAsync(int scheduleId, bool force = false) => await GetTrackDisplayNameForBiblePublicationAsync(scheduleId, null, force);

    public async Task<string> GetTrackDisplayNameForBiblePublicationAsync(int scheduleId, BiblePublicationSchedule? biblePublicationSchedule, bool force = false)
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

            if (scheduleToUse == null)
            {
                return string.Empty;
            }

            var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleToUse.PublicationCode);

            if (hasSectionStructure)
            {
                // Traditional Bible: "Section Name - Track Number" (e.g., "Genesis - 1")
                // Convert SectionCode to int for service call
                if (string.IsNullOrEmpty(scheduleToUse.SectionCode) || 
                    !int.TryParse(scheduleToUse.SectionCode, out var sectionCode))
                {
                    return string.Empty;
                }

                var sectionName = await biblePublicationSectionService.GetSectionNameAsync(
                    scheduleToUse.LanguageCode,
                    scheduleToUse.PublicationCode,
                    sectionCode,
                    cancellationTokenSource.Token);

                if (sectionName == null)
                {
                    return string.Empty;
                }

                return $"{sectionName} - {scheduleToUse.TrackNumber}";
            }
            else
            {
                // Drama/Video: Just the track title
                var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
                    scheduleToUse.LanguageCode,
                    scheduleToUse.PublicationCode,
                    cancellationTokenSource.Token);

                if (publication != null)
                {
                    var track = publication.Tracks.FirstOrDefault(t => t.Number == scheduleToUse.TrackNumber);
                    if (track != null)
                    {
                        return track.Title ?? string.Empty;
                    }
                }

                return string.Empty;
            }
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

