#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Represents a track for Bible publications.
/// Can be either:
/// - Section-based: Links to BiblePublicationSection (traditional Bible publications)
/// - Publication-based: Links directly to BiblePublication (Drama/Video publications)
/// </summary>
[Table("BiblePublicationTracks")]
[Index(nameof(BiblePublicationId), nameof(TrackCode))]
[Index(nameof(BiblePublicationSectionId), nameof(TrackCode), IsUnique = true)]
public sealed class BiblePublicationTrack : IComparable
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Track code from API: chapter number as string for Bible (e.g. "1", "2"), pub value for drama (e.g. "iaey"), track number as string for music/video.
    /// This is the stable identifier used in schedules and for API lookups.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TrackCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Required foreign key to BiblePublication.
    /// All tracks belong to a publication, whether section-based or publication-based.
    /// </summary>
    [Required]
    [ForeignKey(nameof(Publication))]
    public int BiblePublicationId { get; set; }

    [Required]
    public BiblePublication Publication { get; set; } = null!;

    /// <summary>
    /// Optional foreign key to BiblePublicationSection.
    /// Set for section-based tracks (traditional Bible publications).
    /// Null for publication-based tracks (Drama/Video).
    /// </summary>
    [ForeignKey(nameof(Section))]
    public int? BiblePublicationSectionId { get; set; }

    /// <summary>
    /// Optional navigation property to BiblePublicationSection.
    /// Only set for section-based tracks.
    /// </summary>
    public BiblePublicationSection? Section { get; set; }

    /// <summary>
    /// Navigation property to TrackUrl (1:1). CDN URL stored there from pub/section fetch response.
    /// </summary>
    public TrackUrl? TrackUrl { get; set; }

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationTrack other)
        {
            return 1;
        }

        // Compare TrackCode as string, but try to parse as int for numeric comparison when both are numeric
        if (int.TryParse(TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var thisNum) &&
            int.TryParse(other.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var otherNum))
        {
            return thisNum.CompareTo(otherNum);
        }
        return string.Compare(TrackCode, other.TrackCode, StringComparison.OrdinalIgnoreCase);
    }
}
