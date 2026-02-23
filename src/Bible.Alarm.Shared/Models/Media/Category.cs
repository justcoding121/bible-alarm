#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media;

[Table("Categories")]
[Index(nameof(CategoryCode), IsUnique = true)]
public sealed class Category : IComparable
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Stable code (PascalCase, no spaces), e.g. "Bible", "InterviewsAndExperiences".
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>
    /// Junction: publications in this category (many-to-many).
    /// </summary>
    public List<BiblePublications.BiblePublicationCategory> BiblePublicationCategories { get; set; } = [];

    public int CompareTo(object? obj)
    {
        if (obj is not Category other)
        {
            return 1;
        }

        return string.Compare(CategoryCode, other.CategoryCode, StringComparison.Ordinal);
    }
}
