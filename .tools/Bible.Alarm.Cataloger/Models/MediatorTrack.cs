#nullable enable

using System;
using System.Globalization;

namespace Bible.Alarm.Cataloger.Models;

public sealed class MediatorTrack : IComparable, IComparable<MediatorTrack>, IEquatable<MediatorTrack>
{
    public string TrackCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LookUpPath { get; set; } = string.Empty;

    public int CompareTo(MediatorTrack? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (int.TryParse(TrackCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thisNum) &&
            int.TryParse(other.TrackCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var otherNum))
        {
            return thisNum.CompareTo(otherNum);
        }

        return string.Compare(TrackCode, other.TrackCode, StringComparison.OrdinalIgnoreCase);
    }

    public int CompareTo(object? obj) => CompareTo(obj as MediatorTrack);

    public bool Equals(MediatorTrack? other)
    {
        if (other is null)
        {
            return false;
        }

        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj) => Equals(obj as MediatorTrack);

    public override int GetHashCode() =>
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(TrackCode ?? string.Empty), StringComparer.OrdinalIgnoreCase.GetHashCode(Title));

    public static bool operator ==(MediatorTrack? left, MediatorTrack? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(MediatorTrack? left, MediatorTrack? right) => !(left == right);

    public static bool operator <(MediatorTrack? left, MediatorTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(MediatorTrack? left, MediatorTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(MediatorTrack? left, MediatorTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(MediatorTrack? left, MediatorTrack? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}

