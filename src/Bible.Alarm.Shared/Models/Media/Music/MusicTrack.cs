#nullable enable

using System;

namespace Bible.Alarm.Shared.Models.Media.Music;

/// <summary>
/// Represents a music track. Used for compatibility with existing music services.
/// Maps from BiblePublicationTrack for music publications.
/// </summary>
public class MusicTrack : IComparable
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
    public int? OriginalTrackNumber { get; set; }

    public int CompareTo(object? obj) => Number.CompareTo((obj as MusicTrack)?.Number ?? 0);
}
