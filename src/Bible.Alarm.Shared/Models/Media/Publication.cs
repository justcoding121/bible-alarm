#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bible.Alarm.Shared.Models.Media;

public class Publication : IComparable, IEquatable<Publication>
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("PublicationCode")]
    public string PublicationCode { get; set; } = string.Empty;

    public int CompareTo(object? obj)
    {
        if (obj is not Publication other)
        {
            return 1;
        }

        return Name.CompareTo(other.Name);
    }

    public virtual bool Equals(Publication? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Publication);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    public static bool operator ==(Publication? left, Publication? right) =>
        ReferenceEquals(left, right) ||
        left is not null && right is not null && left.Equals(right);

    public static bool operator !=(Publication? left, Publication? right) => !(left == right);

    public static bool operator <(Publication? left, Publication? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(Publication? left, Publication? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(Publication? left, Publication? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(Publication? left, Publication? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}

public class TranslatedPublication : Publication
{
    [Required]
    [ForeignKey(nameof(Language))]
    public int LanguageId { get; set; }

    [Required]
    public virtual Language Language { get; set; } = null!;

    public override bool Equals(Publication? other) =>
        other is TranslatedPublication tp &&
        LanguageId == tp.LanguageId &&
        base.Equals(other);

    public override int GetHashCode() => HashCode.Combine(LanguageId, base.GetHashCode());
}
