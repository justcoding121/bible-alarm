#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Tracks available languages for each publication section by publication code and section code.
/// Used to determine which languages can be fetched on-demand when user changes language.
/// </summary>
[Table("SectionLanguages")]
[Index(nameof(PublicationCode), nameof(SectionCode), nameof(LanguageId), IsUnique = true)]
public sealed class SectionLanguage
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Publication code (e.g., "nwt", "bi12")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PublicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Section code (e.g., "1" for book 1, "gen" for Genesis, section code for dramas)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SectionCode { get; set; } = string.Empty;

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
    /// Foreign key to PublicationLanguage (relates this section language to the publication language)
    /// </summary>
    [Required]
    [ForeignKey(nameof(PublicationLanguage))]
    public int PublicationLanguageId { get; set; }

    /// <summary>
    /// Navigation property to PublicationLanguage
    /// </summary>
    [Required]
    public PublicationLanguage PublicationLanguage { get; set; } = null!;
}
