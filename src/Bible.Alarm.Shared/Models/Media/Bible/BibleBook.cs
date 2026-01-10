using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Bible;

[Table("BibleSection")]
[Index(nameof(BiblePublicationId), nameof(Number), IsUnique = true)]
public sealed class BibleSection : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(1, 66)]
    public int Number { get; set; }

    [Required]
    [ForeignKey(nameof(BiblePublication))]
    public int BiblePublicationId { get; set; }

    [Required]
    public BiblePublication BiblePublication { get; set; } = null!;

    [Required]
    public List<BiblePublicationChapter> Chapters { get; set; } = [];

    public int CompareTo(object obj) => obj is not BibleSection other ? 1 : Number.CompareTo(other.Number);
}
