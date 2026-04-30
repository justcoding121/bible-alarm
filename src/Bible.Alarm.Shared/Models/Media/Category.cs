#nullable enable
using System;
using System.Runtime.CompilerServices;
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

        return CompareTo(other);
    }

    public int CompareTo(Category? other)
    {
        if (other is null)
        {
            return 1;
        }

        return string.Compare(CategoryCode, other.CategoryCode, StringComparison.Ordinal);
    }

    public bool Equals(Category? other) =>
        other is not null &&
        (Id != 0 ? Id == other.Id : ReferenceEquals(this, other));

    public override bool Equals(object? obj) => Equals(obj as Category);

    public override int GetHashCode() =>
        Id != 0 ? Id.GetHashCode() : RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(Category? left, Category? right) =>
        ReferenceEquals(left, right) ||
        left is not null && right is not null && left.Equals(right);

    public static bool operator !=(Category? left, Category? right) => !(left == right);

    public static bool operator <(Category? left, Category? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(Category? left, Category? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(Category? left, Category? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(Category? left, Category? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
