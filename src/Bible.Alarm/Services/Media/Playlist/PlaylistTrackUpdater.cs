#nullable enable
using Bible;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Handles updating schedules when tracks are played or finished.
/// Separated from PlaylistService for better modularity.
/// </summary>
public static class PlaylistTrackUpdater
{
    public static void UpdateMusicTrack(AlarmSchedule schedule, int? nextTrackNumber)
    {
        if (schedule.Music != null && !schedule.Music.Repeat && nextTrackNumber.HasValue)
        {
            schedule.Music.TrackNumber = nextTrackNumber.Value;
        }
    }

    public static void UpdateBiblePublicationTrack(AlarmSchedule schedule, TrackMetadata trackMetadata)
    {
        var biblePublicationSchedule = schedule.BiblePublicationSchedule ??
            throw new InvalidOperationException($"BiblePublicationSchedule is null for schedule {schedule.Id}");

        biblePublicationSchedule.SectionNumber = trackMetadata.SectionNumber;
        biblePublicationSchedule.TrackNumber = trackMetadata.TrackNumber;
        biblePublicationSchedule.LanguageCode = trackMetadata.LanguageCode;
        biblePublicationSchedule.PublicationCode = trackMetadata.PublicationCode;
        biblePublicationSchedule.FinishedDuration = trackMetadata.FinishedDuration;
    }

    public static void UpdateMusicTrackForFinished(AlarmSchedule schedule, int? nextTrackNumber)
    {
        if (schedule.Music != null && !schedule.Music.Repeat && nextTrackNumber.HasValue)
        {
            schedule.Music.TrackNumber = nextTrackNumber.Value;
        }
    }

    public static void UpdateBiblePublicationTrackForFinished(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        KeyValuePair<BibleSection, BibleTrack>? nextTrack)
    {
        var biblePublicationSchedule = schedule.BiblePublicationSchedule ??
            throw new InvalidOperationException($"BiblePublicationSchedule is null for schedule {schedule.Id}");

        if (nextTrack == null || nextTrack.Value.Key == null || nextTrack.Value.Value == null)
        {
            throw new InvalidOperationException("Next track Key or Value is null");
        }

        biblePublicationSchedule.SectionNumber = nextTrack.Value.Key.Number;
        biblePublicationSchedule.TrackNumber = nextTrack.Value.Value.Number;
        biblePublicationSchedule.LanguageCode = trackMetadata.LanguageCode;
        biblePublicationSchedule.PublicationCode = trackMetadata.PublicationCode;
        biblePublicationSchedule.FinishedDuration = TimeSpan.Zero;
    }

    public static void UpdateScheduleForPlayedTrackInternal(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        int? nextTrackNumber)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            UpdateMusicTrack(schedule, nextTrackNumber);
        }
        else
        {
            UpdateBiblePublicationTrack(schedule, trackMetadata);
        }
    }
}

