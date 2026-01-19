#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Tracks available languages for each publication by publication code.
/// Used to determine which languages can be fetched on-demand when user changes language.
/// </summary>
[Table("PublicationLanguages")]
[Index(nameof(PublicationCode), nameof(LanguageId), IsUnique = true)]
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
    /// Foreign key to Language
    /// </summary>
    [Required]
    [ForeignKey(nameof(Language))]
    public int LanguageId { get; set; }

    /// <summary>
    /// Navigation property to Language
    /// </summary>
    [Required]
    public Media.Language Language { get; set; } = null!;

    /// <summary>
    /// Navigation property to SectionLanguages (sections available in this language for this publication)
    /// </summary>
    public List<SectionLanguage> SectionLanguages { get; set; } = [];
}
