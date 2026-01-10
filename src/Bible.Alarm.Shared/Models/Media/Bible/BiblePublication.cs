using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.Bible;

/// <summary>
/// Represents a Bible-related publication including traditional Bible translations,
/// Audio Bible Dramas, Dramatic Bible Readings, and Video publications.
/// </summary>
[Table("BiblePublication")]
[Index(nameof(Code), nameof(LanguageId), IsUnique = true)]
public sealed class BiblePublication : TranslatedPublication
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Books for traditional Bible translations (Book → Chapter structure).
    /// Empty for Drama/Video publications.
    /// </summary>
    [Required]
    public List<BibleBook> Books { get; set; } = [];

    /// <summary>
    /// Tracks for Drama/Video publications (flat structure).
    /// Empty for traditional Bible translations.
    /// </summary>
    [Required]
    public List<BiblePublicationTrack> Tracks { get; set; } = [];
}
