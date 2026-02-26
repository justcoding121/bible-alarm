#nullable enable

using System;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Models.Media.Music;

/// <summary>
/// Represents a music track. Used for compatibility with existing music services.
/// Maps from BiblePublicationTrack for music publications.
/// Order by TrackCode (numeric when both parse as int, otherwise string).
/// </summary>
public class MusicTrack : IComparable
{
    /// <summary>
    /// Track code from the source (e.g. "1", "110", "jwb-201708"). Use for schedule persistence, lookup and ordering.
    /// </summary>
    public string? TrackCode { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public string LookUpPath { get; set; } = string.Empty;

    /// <summary>
    /// Download code used to fetch this track (e.g., "iam-1", "iam-2" for melody music discs).
    /// </summary>
    public string? DownloadCode { get; set; }

    public int CompareTo(object? obj)
    {
        if (obj is not MusicTrack other)
            return 0;
        return CodeComparisonHelper.Compare(TrackCode, other.TrackCode);
    }
}
