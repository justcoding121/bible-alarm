using System;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Models.Media;

public class TrackMetadata
{
    public long ScheduleId { get; set; }
    public DateTimeOffset NotificationTime { get; set; }

    /// <summary>
    /// Explicitly set to true for Bible publication content (both sectioned and non-sectioned).
    /// Set at creation time by PlaylistBibleTrackBuilder.
    /// Music tracks leave this as false (default).
    /// </summary>
    public bool IsBibleContent { get; set; }

    /// <summary>
    /// Determines the play type based on IsBibleContent flag.
    /// This flag is set explicitly when creating the TrackMetadata.
    /// </summary>
    public PlayType PlayType => IsBibleContent ? PlayType.Bible : PlayType.Music;

    public string LanguageCode { get; set; } = string.Empty;
    public string PublicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Computes the LookUpPath at runtime based on PlayType and available parameters.
    /// This replaces storing LookUpPath in the database.
    /// </summary>
    public string LookUpPath => PlayType == PlayType.Bible
        ? LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(LanguageCode, PublicationCode, SectionNumber, TrackNumber)
        : LookUpPathBuilder.BuildMusicTrackLookUpPath(PublicationCode, LanguageCode, TrackNumber);

    public int SectionNumber { get; set; }
    public int TrackNumber { get; set; }

    public TimeSpan FinishedDuration { get; set; }

    public bool IsAlarmMusic => TrackNumber > 0;

    public bool IsLastTrack { get; set; }
}
