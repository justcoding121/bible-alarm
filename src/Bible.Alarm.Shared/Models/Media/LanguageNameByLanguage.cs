#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media;

/// <summary>
/// Localized name for a language by display language code.
/// e.g. "MY" (Malayalam) has Name "Malayalam" for DisplayLanguageCode "E", and "മലയാളം" for "MY".
/// Seeded for "E" only for now; other display languages can be added later.
/// </summary>
[Table("LanguageNamesByLanguage")]
[Index(nameof(LanguageId), nameof(DisplayLanguageCode), IsUnique = true)]
public sealed class LanguageNameByLanguage
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(Language))]
    public int LanguageId { get; set; }

    public Language Language { get; set; } = null!;

    /// <summary>
    /// The locale in which this name is written (e.g. "E" for English).
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string DisplayLanguageCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
}
