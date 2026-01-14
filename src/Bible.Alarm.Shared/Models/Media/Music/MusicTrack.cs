#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media.Music;

[Table("MusicTracks")]
public sealed class MusicTrack : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Range(1, 500)]
    public int Number { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Download code used to fetch this track (e.g., "iam-1", "iam-2" for melody music discs).
    /// This is needed for melody music publications that use multiple disc codes.
    /// For regular publications, this will be the same as the publication code.
    /// </summary>
    [MaxLength(50)]
    public string? DownloadCode { get; set; }

    /// <summary>
    /// Original track number from the API response (within the disc).
    /// This is needed for melody music with multiple discs, where the API expects the track number within that specific disc,
    /// not the sequential track number across all discs.
    /// For regular publications, this will be the same as Number.
    /// </summary>
    public int? OriginalTrackNumber { get; set; }

    public int CompareTo(object? obj)
    {
        if (obj is not MusicTrack other)
        {
            return 1;
        }

        return Number.CompareTo(other.Number);
    }
}
