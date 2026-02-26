#nullable enable
using Bible.Alarm.Shared.Helpers;
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
    public static void UpdateMusicTrack(AlarmSchedule schedule, string? nextTrackCode, string? nextSectionCode = null)
    {
        if (schedule.Music != null && !schedule.Music.Repeat && !string.IsNullOrWhiteSpace(nextTrackCode))
        {
            schedule.Music.TrackCode = nextTrackCode;
            if (!string.IsNullOrWhiteSpace(nextSectionCode))
            {
                schedule.Music.SectionCode = nextSectionCode;
            }
        }
    }

    public static void UpdateBiblePublicationTrack(AlarmSchedule schedule, TrackMetadata trackMetadata)
    {
        var biblePublicationSchedule = schedule.BiblePublicationSchedule ??
            throw new InvalidOperationException($"BiblePublicationSchedule is null for schedule {schedule.Id}");

        // Preserve section code; for non-sectioned publications it will be null/empty.
        biblePublicationSchedule.SectionCode = string.IsNullOrWhiteSpace(trackMetadata.SectionCode)
            ? null
            : trackMetadata.SectionCode;
        biblePublicationSchedule.TrackCode = trackMetadata.TrackCode;
        biblePublicationSchedule.LanguageCode = trackMetadata.LanguageCode;
        biblePublicationSchedule.PublicationCode = trackMetadata.PublicationCode;
        biblePublicationSchedule.FinishedDuration = trackMetadata.FinishedDuration;
    }

    public static void UpdateMusicTrackForFinished(AlarmSchedule schedule, string? nextTrackCode, string? nextSectionCode = null)
    {
        if (schedule.Music != null && !schedule.Music.Repeat && !string.IsNullOrWhiteSpace(nextTrackCode))
        {
            schedule.Music.TrackCode = nextTrackCode;
            if (!string.IsNullOrWhiteSpace(nextSectionCode))
            {
                schedule.Music.SectionCode = nextSectionCode;
            }
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
        biblePublicationSchedule.TrackCode = TrackCodeHelper.GetFromTrack(nextTrack.Value.Value);
        biblePublicationSchedule.LanguageCode = trackMetadata.LanguageCode;
        biblePublicationSchedule.PublicationCode = trackMetadata.PublicationCode;
        biblePublicationSchedule.FinishedDuration = TimeSpan.Zero;
    }

    public static void UpdateScheduleForPlayedTrackInternal(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        string? nextTrackCode,
        string? nextSectionCode = null)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            UpdateMusicTrack(schedule, nextTrackCode, nextSectionCode);
        }
        else
        {
            UpdateBiblePublicationTrack(schedule, trackMetadata);
        }
    }
}

