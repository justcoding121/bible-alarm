#nullable enable
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles schedule updates for track navigation.
/// </summary>
public sealed class ScheduleUpdater(
    IAlarmScheduleService alarmScheduleService,
    CancellationToken cancellationToken)
{
    /// <summary>
    /// Updates schedule to the next track.
    /// For non-sectioned publications, section will be null.
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleToNextTrackAsync(int scheduleId, KeyValuePair<BiblePublicationSection?, BiblePublicationTrack> next)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BiblePublicationSchedule ?? throw new ArgumentException($"BiblePublicationSchedule is null for schedule {scheduleId}");
                // For non-sectioned publications, keep section as null
                // Use SectionCode from the section, or null if section is null
                brs.SectionCode = next.Key?.SectionCode;
                brs.TrackNumber = next.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            cancellationToken);
    }

    /// <summary>
    /// Updates schedule to the previous track.
    /// For non-sectioned publications, section will be null.
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleToPreviousTrackAsync(int scheduleId, KeyValuePair<BiblePublicationSection?, BiblePublicationTrack> previous)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BiblePublicationSchedule ?? throw new ArgumentException($"BiblePublicationSchedule is null for schedule {scheduleId}");
                // For non-sectioned publications, keep section as null
                // Use SectionCode from the section, or null if section is null
                brs.SectionCode = previous.Key?.SectionCode;
                brs.TrackNumber = previous.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets schedule with Bible reading validation.
    /// </summary>
    public async Task<AlarmSchedule> GetScheduleWithBiblePublicationAsync(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, true, cancellationToken);

        if (schedule?.BiblePublicationSchedule == null)
        {
            throw new ArgumentException($"BiblePublicationSchedule is null for schedule {scheduleId}");
        }

        return schedule;
    }
}
