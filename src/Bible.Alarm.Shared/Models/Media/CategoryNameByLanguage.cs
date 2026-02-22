#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media;

/// <summary>
/// Localized name for a category by language code.
/// Seeded for "E" (English) by default; other languages can be added later.
/// </summary>
[Table("CategoryNamesByLanguage")]
[Index(nameof(CategoryId), nameof(LanguageCode), IsUnique = true)]
public sealed class CategoryNameByLanguage
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(Category))]
    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
}
