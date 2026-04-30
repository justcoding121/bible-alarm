#nullable enable

using System;
using System.Runtime.CompilerServices;
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

    public int CompareTo(MusicTrack? other)
    {
        if (other is null)
        {
            return 1;
        }

        return CodeComparisonHelper.Compare(TrackCode, other.TrackCode);
    }

    public int CompareTo(object? obj) => obj is MusicTrack other ? CompareTo(other) : 1;

    public bool Equals(MusicTrack? other) =>
        other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => Equals(obj as MusicTrack);

    public override int GetHashCode() =>
        TrackCode is null ? RuntimeHelpers.GetHashCode(this) : StringComparer.Ordinal.GetHashCode(TrackCode);

    public static bool operator ==(MusicTrack? left, MusicTrack? right) =>
        ReferenceEquals(left, right) ||
        left is not null && right is not null && left.Equals(right);

    public static bool operator !=(MusicTrack? left, MusicTrack? right) => !(left == right);

    public static bool operator <(MusicTrack? left, MusicTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(MusicTrack? left, MusicTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(MusicTrack? left, MusicTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(MusicTrack? left, MusicTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
