#nullable enable
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
public sealed class BiblePublication : TranslatedPublication
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to Category
    /// </summary>
    [Required]
    [ForeignKey(nameof(Category))]
    public int CategoryId { get; set; }

    /// <summary>
    /// Navigation property to Category
    /// </summary>
    [Required]
    public Media.Category Category { get; set; } = null!;

    /// <summary>
    /// Optional foreign key to Language
    /// </summary>
    [ForeignKey(nameof(Language))]
    public new int? LanguageId { get; set; }

    /// <summary>
    /// Optional navigation property to Language
    /// </summary>
    public new Media.Language? Language { get; set; }

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

    /// <summary>
    /// Indicates if this publication is a video publication
    /// </summary>
    [Required]
    public bool IsVideo { get; set; }
}
