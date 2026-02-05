#nullable enable
using System;
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
    /// Optional natural key for drama/video tracks (e.g., "pub-dwj_E_1_AUDIO").
    /// Used for more precise lookup when available.
    /// </summary>
    public string? NaturalKey { get; set; }

    /// <summary>
    /// Download code used to fetch this track (e.g., "iam-1", "iam-2" for melody music discs).
    /// This is needed for melody music publications that use multiple disc codes.
    /// For regular publications, this will be null and the publication code will be used.
    /// </summary>
    public string? DownloadCode { get; set; }

    /// <summary>
    /// Original track number from the API response (within the disc).
    /// This is needed for melody music with multiple discs, where the API expects the track number within that specific disc.
    /// For regular publications, this will be null and TrackNumber will be used.
    /// </summary>
    public int? OriginalTrackNumber { get; set; }

    /// <summary>
    /// The lookup path (query string) for refreshing the URL from the API.
    /// Must be set from the media index (UrlConstructionService) before use. We only play harvested tracks.
    /// </summary>
    private string? _lookUpPath;

    public string LookUpPath
    {
        get
        {
            if (string.IsNullOrEmpty(_lookUpPath))
            {
                throw new InvalidOperationException(
                    "TrackMetadata.LookUpPath was not set. Lookup path must be loaded from the media index (IUrlConstructionService.ConstructTrackLookUpPathAsync) before playback.");
            }
            return _lookUpPath;
        }
        set => _lookUpPath = value;
    }

    public string? SectionCode { get; set; }
    public int TrackNumber { get; set; }

    public TimeSpan FinishedDuration { get; set; }

    public bool IsAlarmMusic => TrackNumber > 0;

    public bool IsLastTrack { get; set; }
}
