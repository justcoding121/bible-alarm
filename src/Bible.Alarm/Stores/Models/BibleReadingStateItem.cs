#nullable enable
namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Bible reading schedule item DTO for Fluxor state.
/// Contains all Bible reading schedule properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public class BibleReadingStateItem : IComparable
{
    public int Id { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string PublicationCode { get; set; } = string.Empty;
    public int BookNumber { get; set; }
    public int ChapterNumber { get; set; }
    public TimeSpan FinishedDuration { get; set; }
    public int AlarmScheduleId { get; set; }

    /// <summary>
    /// Translation name (language name) for display purposes.
    /// This is populated during bootstrap from language dictionary.
    /// Not persisted to database.
    /// </summary>
    public string? TranslationName { get; set; }

    /// <summary>
    /// Compare by ID for ObservableHashSet ordering.
    /// </summary>
    public int CompareTo(object? obj)
    {
        if (obj is not BibleReadingStateItem other)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }
}

