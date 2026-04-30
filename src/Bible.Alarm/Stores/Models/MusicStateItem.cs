#nullable enable

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Music item DTO for Fluxor state.
/// Contains all music properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// Music type (melody vs. vocal) is inferred from LanguageCode: NULL = melody, non-NULL = vocal.
/// </summary>
public sealed class MusicStateItem : IComparable, IComparable<MusicStateItem>, IEquatable<MusicStateItem>
{
    public int Id { get; set; }
    public string PublicationCode { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string? SectionCode { get; set; } // Section code for music publications with sections
    public string TrackCode { get; set; } = string.Empty;
    public bool Repeat { get; set; }
    public int AlarmScheduleId { get; set; }

    /// <summary>
    /// Language name for display purposes (for vocals only).
    /// This is populated from the list item when user selects a language.
    /// Not persisted to database.
    /// </summary>
    public string? LanguageName { get; set; }

    /// <summary>
    /// Language direction for RTL/LTR display (for vocals only).
    /// Values: "ltr" (left-to-right) or "rtl" (right-to-left).
    /// This is populated from the list item when user selects a language.
    /// Not persisted to database.
    /// </summary>
    public string? LanguageDirection { get; set; }

    /// <summary>
    /// Publication name (song section name) for display purposes (for vocals only).
    /// This is populated from the list item when user selects a song section.
    /// Not persisted to database.
    /// </summary>
    public string? PublicationName { get; set; }

    /// <summary>
    /// Section name for display purposes (for music publications with sections like Kingdom Melodies).
    /// This is populated from the list item when user selects a section.
    /// Not persisted to database.
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// Track name for display purposes.
    /// This is populated from the list item when user selects a track.
    /// Not persisted to database.
    /// </summary>
    public string? TrackName { get; set; }

    /// <summary>
    /// Compare by ID for ObservableHashSet ordering.
    /// </summary>
    public int CompareTo(MusicStateItem? other) =>
        other is null ? 1 : Id.CompareTo(other.Id);

    public int CompareTo(object? obj) => CompareTo(obj as MusicStateItem);

    public bool Equals(MusicStateItem? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as MusicStateItem);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(MusicStateItem? left, MusicStateItem? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(MusicStateItem? left, MusicStateItem? right) => !(left == right);

    public static bool operator <(MusicStateItem? left, MusicStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(MusicStateItem? left, MusicStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(MusicStateItem? left, MusicStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(MusicStateItem? left, MusicStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
