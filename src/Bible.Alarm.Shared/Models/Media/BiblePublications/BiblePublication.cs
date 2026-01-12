using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Represents a Bible-related publication including traditional Bible publications,
/// Audio Bible Dramas, Dramatic Bible Readings, and Video publications.
/// </summary>
[Table("BiblePublications")]
[Index(nameof(Code), nameof(LanguageId), IsUnique = true)]
public sealed class BiblePublication : TranslatedPublication
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Sections for traditional Bible publications (Section → Track structure).
    /// Empty for Drama/Video publications.
    /// </summary>
    [Required]
    public List<BiblePublicationSection> Sections { get; set; } = [];

    /// <summary>
    /// Tracks for Drama/Video publications (flat structure).
    /// Empty for traditional Bible publications.
    /// </summary>
    [Required]
    public List<BiblePublicationTrack> Tracks { get; set; } = [];
}
