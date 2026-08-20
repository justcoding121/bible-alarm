#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bible.Alarm.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Tracks available languages for each publication by publication code.
/// Used to determine which languages can be fetched on-demand when user changes language.
/// </summary>
[Table("PublicationLanguages")]
[Index(nameof(PublicationCode), nameof(LanguageId), nameof(CategoryId), IsUnique = true)]
public sealed class PublicationLanguage
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Publication code (e.g., "nwt", "bi12", "osg", "Dramas")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PublicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Language (nullable for publications without language, e.g., instrumental music like "iam")
    /// </summary>
    [ForeignKey(nameof(Language))]
    public int? LanguageId { get; set; }

    public Media.Language? Language { get; set; }

    /// <summary>
    /// The type of cataloging logic to use for this publication.
    /// Determined during discovery based on publication code and category.
    /// </summary>
    public CatalogType? CatalogType { get; set; }

    [Required]
    [ForeignKey(nameof(Category))]
    public int CategoryId { get; set; }

    [Required]
    public Media.Category Category { get; set; } = null!;

    /// <summary>
    /// True when this publication is music (vocal, melody, or music-flag). Set during discovery/catalog.
    /// Used for prev/next cross-pub logic when BiblePublication is not yet cataloged.
    /// </summary>
    public bool IsMusic { get; set; }

    public List<SectionLanguage> SectionLanguages { get; set; } = [];
}
