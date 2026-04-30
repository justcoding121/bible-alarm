#nullable enable

using System;

namespace Bible.Alarm.Cataloger.Models;

public sealed class VideoEpisode : IComparable, IComparable<VideoEpisode>, IEquatable<VideoEpisode>
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LookUpPath { get; set; } = string.Empty;
    public double Duration { get; set; }

    public int CompareTo(VideoEpisode? other) =>
        other is null ? 1 : Number.CompareTo(other.Number);

    public int CompareTo(object? obj) => CompareTo(obj as VideoEpisode);

    public bool Equals(VideoEpisode? other) =>
        other is not null && Number == other.Number;

    public override bool Equals(object? obj) => Equals(obj as VideoEpisode);

    public override int GetHashCode() => Number.GetHashCode();

    public static bool operator ==(VideoEpisode? left, VideoEpisode? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(VideoEpisode? left, VideoEpisode? right) => !(left == right);

    public static bool operator <(VideoEpisode? left, VideoEpisode? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(VideoEpisode? left, VideoEpisode? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(VideoEpisode? left, VideoEpisode? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(VideoEpisode? left, VideoEpisode? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
