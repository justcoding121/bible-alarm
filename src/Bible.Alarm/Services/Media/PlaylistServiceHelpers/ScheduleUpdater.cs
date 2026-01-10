#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

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
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleToNextTrackAsync(int scheduleId, KeyValuePair<BibleSection, BiblePublicationTrack> next)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BiblePublicationSchedule ?? throw new ArgumentException($"BiblePublicationSchedule is null for schedule {scheduleId}");
                brs.SectionNumber = next.Key.Number;
                brs.TrackNumber = next.Value.Number;
                brs.FinishedDuration = TimeSpan.Zero;
            },
            cancellationToken);
    }

    /// <summary>
    /// Updates schedule to the previous track.
    /// </summary>
    public async Task<AlarmSchedule> UpdateScheduleToPreviousTrackAsync(int scheduleId, KeyValuePair<BibleSection, BiblePublicationTrack> previous)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            s =>
            {
                var brs = s.BiblePublicationSchedule ?? throw new ArgumentException($"BiblePublicationSchedule is null for schedule {scheduleId}");
                brs.SectionNumber = previous.Key.Number;
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
