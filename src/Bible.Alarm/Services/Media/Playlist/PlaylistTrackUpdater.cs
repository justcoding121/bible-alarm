#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;

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

        // Convert SectionNumber (int) to SectionCode (string)
        // For non-sectioned publications (SectionNumber == 0), set SectionCode to null
        biblePublicationSchedule.SectionCode = trackMetadata.SectionNumber > 0 ? trackMetadata.SectionNumber.ToString() : null;
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
        KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>? nextTrack)
    {
        var biblePublicationSchedule = schedule.BiblePublicationSchedule ??
            throw new InvalidOperationException($"BiblePublicationSchedule is null for schedule {schedule.Id}");

        if (nextTrack == null || nextTrack.Value.Value == null)
        {
            throw new InvalidOperationException("Next track Value is null");
        }

        // For non-sectioned publications, Key (section) will be null
        // Use SectionCode from the section, or null if section is null
        biblePublicationSchedule.SectionCode = nextTrack.Value.Key?.SectionCode;
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

