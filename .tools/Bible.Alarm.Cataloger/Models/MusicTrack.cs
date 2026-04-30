#nullable enable

using System;

namespace Bible.Alarm.Cataloger.Models;

public sealed class MusicTrack : IComparable, IComparable<MusicTrack>, IEquatable<MusicTrack>
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    // LookUpPath is no longer stored in the database - it's computed at runtime
    // Keeping the property for backward compatibility with JSON deserialization
    public string LookUpPath { get; set; } = string.Empty;

    /// <summary>
    /// Download code used to fetch this track (e.g., "iam-1", "iam-2" for melody music discs).
    /// This is needed for melody music publications that use multiple disc codes.
    /// For regular publications, this will be the same as the publication code.
    /// </summary>
    public string? DownloadCode { get; set; }

    /// <summary>
    /// Original track number from the API response (within the disc).
    /// This is needed for melody music with multiple discs, where the API expects the track number within that specific disc,
    /// not the sequential track number across all discs.
    /// For regular publications, this will be the same as Number.
    /// </summary>
    public int? OriginalTrackCode { get; set; }

    public int CompareTo(MusicTrack? other) =>
        other is null ? 1 : Number.CompareTo(other.Number);

    public int CompareTo(object? obj) => CompareTo(obj as MusicTrack);

    public bool Equals(MusicTrack? other) =>
        other is not null && Number == other.Number;

    public override bool Equals(object? obj) => Equals(obj as MusicTrack);

    public override int GetHashCode() => Number.GetHashCode();

    public static bool operator ==(MusicTrack? left, MusicTrack? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

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
