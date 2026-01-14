#nullable enable

using System;
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
[Index(nameof(BiblePublicationId), nameof(Number))]
[Index(nameof(BiblePublicationSectionId), nameof(Number), IsUnique = true)]
public sealed class BiblePublicationTrack : IComparable
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

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationTrack other)
        {
            return 1;
        }

        return Number.CompareTo(other.Number);
    }
}
