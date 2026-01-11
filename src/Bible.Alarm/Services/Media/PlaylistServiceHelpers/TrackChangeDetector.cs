#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles detection of track changes.
/// </summary>
public sealed class TrackChangeDetector(
    IAlarmScheduleService alarmScheduleService,
    CancellationToken cancellationToken)
{
    /// <summary>
    /// Checks if the track has changed for a bible reading.
    /// </summary>
    public async Task<bool> CheckIfTrackChanged(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType != PlayType.Bible)
        {
            return false;
        }

        var scheduleBeforeUpdate = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, false, true, cancellationToken);

        if (scheduleBeforeUpdate?.BiblePublicationSchedule == null)
        {
            return false;
        }

        var biblePublicationSchedule = scheduleBeforeUpdate.BiblePublicationSchedule;
        return biblePublicationSchedule.SectionNumber != trackMetadata.SectionNumber ||
               biblePublicationSchedule.TrackNumber != trackMetadata.TrackNumber;
    }
}
