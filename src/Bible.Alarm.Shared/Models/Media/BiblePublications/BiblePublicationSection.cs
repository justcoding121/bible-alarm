#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media.BiblePublications;

[Table("BiblePublicationSections")]
[Index(nameof(BiblePublicationId), IsUnique = false)]
public sealed class BiblePublicationSection : IComparable, IEquatable<BiblePublicationSection>
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Section code (e.g., "1" for book 1, "gen" for Genesis, section code for dramas).
    /// Matches SectionLanguage.SectionCode for consistency.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SectionCode { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(BiblePublication))]
    public int BiblePublicationId { get; set; }

    [Required]
    public BiblePublication BiblePublication { get; set; } = null!;

    [Required]
    public List<BiblePublicationTrack> Tracks { get; set; } = [];

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationSection other)
        {
            return 1;
        }

        return CompareTo(other);
    }

    public int CompareTo(BiblePublicationSection? other)
    {
        if (other is null)
        {
            return 1;
        }

        // IMPORTANT:
        // Section codes are stored/treated as strings throughout the app.
        // The only place we interpret them numerically is for ordering (natural sort).
        return SectionCodeHelper.SectionCodeComparer.Compare(SectionCode, other.SectionCode);
    }

    public bool Equals(BiblePublicationSection? other) =>
        other is not null &&
        (Id != 0 ? Id == other.Id : ReferenceEquals(this, other));

    public override bool Equals(object? obj) => Equals(obj as BiblePublicationSection);

    public override int GetHashCode() =>
        Id != 0 ? Id.GetHashCode() : RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(BiblePublicationSection? left, BiblePublicationSection? right) =>
        ReferenceEquals(left, right) ||
        left is not null && right is not null && left.Equals(right);

    public static bool operator !=(BiblePublicationSection? left, BiblePublicationSection? right) => !(left == right);

    public static bool operator <(BiblePublicationSection? left, BiblePublicationSection? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(BiblePublicationSection? left, BiblePublicationSection? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(BiblePublicationSection? left, BiblePublicationSection? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(BiblePublicationSection? left, BiblePublicationSection? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
