#nullable enable
using Bible.Alarm.Shared.Helpers;
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
    public async Task<AlarmSchedule> UpdateScheduleToNextTrackAsync(int scheduleId, TrackNavigationResult next)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BiblePublicationSchedule ?? throw new ArgumentException($"BiblePublicationSchedule is null for schedule {scheduleId}");
                brs.SectionCode = next.Section?.SectionCode;
                brs.TrackCode = TrackCodeHelper.GetFromTrack(next.Track);
                brs.FinishedDuration = TimeSpan.Zero;
                brs.PublicationCode = next.PublicationCode;
            },
            cancellationToken);
    }

    /// <summary>
    /// Updates schedule to the previous track.
    /// For non-sectioned publications, section will be null.
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleToPreviousTrackAsync(int scheduleId, TrackNavigationResult previous)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BiblePublicationSchedule ?? throw new ArgumentException($"BiblePublicationSchedule is null for schedule {scheduleId}");
                brs.SectionCode = previous.Section?.SectionCode;
                brs.TrackCode = TrackCodeHelper.GetFromTrack(previous.Track);
                brs.FinishedDuration = TimeSpan.Zero;
                brs.PublicationCode = previous.PublicationCode;
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
