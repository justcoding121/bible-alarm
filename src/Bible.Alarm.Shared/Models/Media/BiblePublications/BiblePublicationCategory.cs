#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

/// <summary>
/// Junction table for many-to-many: a publication can belong to many categories.
/// </summary>
[Table("BiblePublicationCategories")]
[Index(nameof(BiblePublicationId), nameof(CategoryId), IsUnique = true)]
public sealed class BiblePublicationCategory
{
    [Required]
    [ForeignKey(nameof(BiblePublication))]
    public int BiblePublicationId { get; set; }

    public BiblePublication BiblePublication { get; set; } = null!;

    [Required]
    [ForeignKey(nameof(Category))]
    public int CategoryId { get; set; }

    public Media.Category Category { get; set; } = null!;
}
