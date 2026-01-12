#nullable enable

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Bible reading schedule item DTO for Fluxor state.
/// Contains all Bible reading schedule properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public sealed class BiblePublicationStateItem : IComparable
{
    public int Id { get; set; }

    public string LanguageCode { get; set; } = string.Empty;
    public string PublicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Section number for traditional Bible readings (1-66).
    /// Null for drama publications which don't have sections.
    /// Use PublicationTypeHelper.HasSectionStructure() to check if this applies.
    /// </summary>
    public int? SectionNumber { get; set; }

    public int TrackNumber { get; set; }
    public TimeSpan FinishedDuration { get; set; }
    public int AlarmScheduleId { get; set; }

    /// <summary>
    /// Language name for display purposes.
    /// This is populated from the list item when user selects a language.
    /// Not persisted to database.
    /// </summary>
    public string? LanguageName { get; set; }

    /// <summary>
    /// Language direction ("ltr" or "rtl") for display purposes.
    /// This is populated from the list item when user selects a language.
    /// Not persisted to database.
    /// </summary>
    public string? LanguageDirection { get; set; }

    /// <summary>
    /// Publication name (publication name) for display purposes.
    /// This is populated from the list item when user selects a publication.
    /// Not persisted to database.
    /// </summary>
    public string? PublicationName { get; set; }

    /// <summary>
    /// Section name for display purposes.
    /// This is populated from the list item when user selects a section.
    /// Not persisted to database.
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// Track title for display purposes (used for drama publications).
    /// This is populated from the list item when user selects a track.
    /// Not persisted to database.
    /// </summary>
    public string? TrackTitle { get; set; }

    /// <summary>
    /// Compare by ID for ObservableHashSet ordering.
    /// </summary>
    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationStateItem other)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }
}

