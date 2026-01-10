#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Bible;

[Table("BiblePublicationTrack")]
[Index(nameof(BiblePublicationSectionId), nameof(Number), IsUnique = true)]
public sealed class BiblePublicationTrack : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Range(1, 150)]
    public int Number { get; set; }

    public string Title => $"Track {Number}";

    public AudioSource? Source { get; set; }

    [Required]
    [ForeignKey(nameof(Section))]
    public int BiblePublicationSectionId { get; set; }

    [Required]
    public BiblePublicationSection Section { get; set; } = null!;

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationTrack other)
        {
            return 1;
        }

        return Number.CompareTo(other.Number);
    }
}
