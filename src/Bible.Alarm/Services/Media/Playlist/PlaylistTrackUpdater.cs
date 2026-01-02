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

    public static void UpdateBibleReadingTrack(AlarmSchedule schedule, TrackMetadata trackMetadata)
    {
        var bibleReadingSchedule = schedule.BibleReadingSchedule ??
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");

        bibleReadingSchedule.BookNumber = trackMetadata.BookNumber;
        bibleReadingSchedule.ChapterNumber = trackMetadata.ChapterNumber;
        bibleReadingSchedule.LanguageCode = trackMetadata.LanguageCode;
        bibleReadingSchedule.PublicationCode = trackMetadata.PublicationCode;
        bibleReadingSchedule.FinishedDuration = trackMetadata.FinishedDuration;
    }

    public static void UpdateMusicTrackForFinished(AlarmSchedule schedule, int? nextTrackNumber)
    {
        if (schedule.Music != null && !schedule.Music.Repeat && nextTrackNumber.HasValue)
        {
            schedule.Music.TrackNumber = nextTrackNumber.Value;
        }
    }

    public static void UpdateBibleReadingTrackForFinished(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        KeyValuePair<BibleBook, BibleChapter>? nextChapter)
    {
        var bibleReadingSchedule = schedule.BibleReadingSchedule ??
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {schedule.Id}");

        if (nextChapter == null || nextChapter.Value.Key == null || nextChapter.Value.Value == null)
        {
            throw new InvalidOperationException("Next chapter Key or Value is null");
        }

        bibleReadingSchedule.BookNumber = nextChapter.Value.Key.Number;
        bibleReadingSchedule.ChapterNumber = nextChapter.Value.Value.Number;
        bibleReadingSchedule.LanguageCode = trackMetadata.LanguageCode;
        bibleReadingSchedule.PublicationCode = trackMetadata.PublicationCode;
        bibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
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
            UpdateBibleReadingTrack(schedule, trackMetadata);
        }
    }
}

