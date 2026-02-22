#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Represents a Bible-related publication including traditional Bible publications,
/// Audio Bible Dramas, Dramatic Bible Readings, and Video publications.
/// A publication can belong to many categories via BiblePublicationCategories.
/// </summary>
[Table("BiblePublications")]
public sealed class BiblePublication : TranslatedPublication
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Many-to-many: categories this publication belongs to.
    /// </summary>
    public List<BiblePublicationCategory> BiblePublicationCategories { get; set; } = [];

    /// <summary>
    /// First category (when loaded). Use when a single category is expected (e.g. Music).
    /// </summary>
    [NotMapped]
    public Media.Category? PrimaryCategory => BiblePublicationCategories.Count > 0 ? BiblePublicationCategories[0].Category : null;

    /// <summary>
    /// First category id (when loaded). Use when a single category is expected.
    /// </summary>
    [NotMapped]
    public int PrimaryCategoryId => BiblePublicationCategories.Count > 0 ? BiblePublicationCategories[0].CategoryId : 0;

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
