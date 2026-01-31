#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using System.Collections.Concurrent;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles detection of track changes.
/// </summary>
public sealed class TrackChangeDetector(
    IAlarmScheduleService alarmScheduleService,
    CancellationToken cancellationToken)
{
    private readonly record struct BibleTrackSignature(int SectionNumber, int TrackNumber);
    private readonly ConcurrentDictionary<int, BibleTrackSignature> lastKnownBibleTrackByScheduleId = new();

    /// <summary>
    /// Updates the in-memory last-known bible track for a schedule.
    /// Call this after persisting schedule changes to avoid re-reading ScheduleDbContext on every track update.
    /// </summary>
    public void SetLastKnownBibleTrack(int scheduleId, int sectionNumber, int trackNumber)
    {
        if (scheduleId <= 0)
        {
            return;
        }

        lastKnownBibleTrackByScheduleId[scheduleId] = new BibleTrackSignature(sectionNumber, trackNumber);
    }

    /// <summary>
    /// Checks if the track has changed for a bible reading.
    /// </summary>
    public async Task<bool> CheckIfTrackChanged(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType != PlayType.Bible)
        {
            return false;
        }

        var scheduleId = (int)trackMetadata.ScheduleId;
        if (scheduleId <= 0)
        {
            return false;
        }

        var current = new BibleTrackSignature(trackMetadata.SectionNumber, trackMetadata.TrackNumber);
        if (lastKnownBibleTrackByScheduleId.TryGetValue(scheduleId, out var cached))
        {
            return cached != current;
        }

        var scheduleBeforeUpdate = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, true, cancellationToken);

        if (scheduleBeforeUpdate?.BiblePublicationSchedule == null)
        {
            return false;
        }

        var biblePublicationSchedule = scheduleBeforeUpdate.BiblePublicationSchedule;
        // Convert SectionCode to int for comparison
        var scheduleSectionNumber = !string.IsNullOrEmpty(biblePublicationSchedule.SectionCode) && int.TryParse(biblePublicationSchedule.SectionCode, out var num) ? num : 0;
        var before = new BibleTrackSignature(scheduleSectionNumber, biblePublicationSchedule.TrackNumber);
        lastKnownBibleTrackByScheduleId[scheduleId] = before;

        return before != current;
    }
}
