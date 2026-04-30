using System;

namespace Bible.Alarm.Cataloger.Models.BiblePublications;

public sealed class BiblePublicationSection : IComparable, IComparable<BiblePublicationSection>, IEquatable<BiblePublicationSection>
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }

    public int CompareTo(BiblePublicationSection? other) =>
        other is null ? 1 : Number.CompareTo(other.Number);

    public int CompareTo(object? obj) => CompareTo(obj as BiblePublicationSection);

    public bool Equals(BiblePublicationSection? other) =>
        other is not null && Number == other.Number;

    public override bool Equals(object? obj) => Equals(obj as BiblePublicationSection);

    public override int GetHashCode() => Number.GetHashCode();

    public static bool operator ==(BiblePublicationSection? left, BiblePublicationSection? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

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
