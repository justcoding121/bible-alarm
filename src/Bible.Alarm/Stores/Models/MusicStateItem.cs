#nullable enable
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Music item DTO for Fluxor state.
/// Contains all music properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public sealed class MusicStateItem : IComparable
{
    public int Id { get; set; }
    public MusicType MusicType { get; set; }
    public string PublicationCode { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string? SectionCode { get; set; } // Section code for music publications with sections
    public int TrackNumber { get; set; }
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
    /// Track name for display purposes.
    /// This is populated from the list item when user selects a track.
    /// Not persisted to database.
    /// </summary>
    public string? TrackName { get; set; }

    /// <summary>
    /// Compare by ID for ObservableHashSet ordering.
    /// </summary>
    public int CompareTo(object? obj)
    {
        if (obj is not MusicStateItem other)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }
}

