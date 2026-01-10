#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Bible;

/// <summary>
/// Represents a track/episode for Drama or Video publications.
/// Links directly to BiblePublication without an intermediate Book level.
/// </summary>
[Table("PublicationTrack")]
[Index(nameof(BiblePublicationId), nameof(Number), IsUnique = true)]
public sealed class PublicationTrack : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Number { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    public AudioSource? Source { get; set; }

    [Required]
    [ForeignKey(nameof(Publication))]
    public int BiblePublicationId { get; set; }

    [Required]
    public BiblePublication Publication { get; set; } = null!;

    public int CompareTo(object? obj)
    {
        if (obj is not PublicationTrack other)
        {
            return 1;
        }

        return Number.CompareTo(other.Number);
    }
}
