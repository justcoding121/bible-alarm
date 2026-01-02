#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles detection of track changes.
/// </summary>
public sealed class TrackChangeDetector(
    ILogger logger,
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

        if (scheduleBeforeUpdate?.BibleReadingSchedule == null)
        {
            return false;
        }

        var bibleReadingSchedule = scheduleBeforeUpdate.BibleReadingSchedule;
        return bibleReadingSchedule.BookNumber != trackMetadata.BookNumber ||
               bibleReadingSchedule.ChapterNumber != trackMetadata.ChapterNumber;
    }
}
