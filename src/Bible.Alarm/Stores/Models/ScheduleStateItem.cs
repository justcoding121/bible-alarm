#nullable enable
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Schedule item DTO for Fluxor state.
/// Contains all schedule properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public class ScheduleStateItem : IComparable
{
    // Schedule properties
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int Second { get; set; }
    public DaysOfWeek DaysOfWeek { get; set; }
    public bool NotificationEnabled { get; set; }
    public bool MusicEnabled { get; set; }
    public int SnoozeMinutes { get; set; }
    public int NumberOfChaptersToRead { get; set; }
    public bool AlwaysPlayFromStart { get; set; }
    public PlayType CurrentPlayItem { get; set; }
    public long LatestAlarmNotificationId { get; set; }

    // Computed properties
    public int MeridianHour => Meridian == Meridian.Am ? Hour == 0 ? 12 : Hour : Hour == 12 ? 12 : Hour % 12;
    public Meridian Meridian => Hour < 12 ? Meridian.Am : Meridian.Pm;
    public string TimeText => $"{MeridianHour:D2}:{Minute:D2}";

    // Bible Reading Schedule properties (flattened)
    public int? BibleReadingScheduleId { get; set; }
    public string? BibleReadingLanguageCode { get; set; }
    public string? BibleReadingPublicationCode { get; set; }
    public int? BibleReadingBookNumber { get; set; }
    public int? BibleReadingChapterNumber { get; set; }
    public TimeSpan? BibleReadingFinishedDuration { get; set; }

    // Music properties (flattened)
    public int? MusicId { get; set; }
    public MusicType? MusicType { get; set; }
    public string? MusicPublicationCode { get; set; }
    public string? MusicLanguageCode { get; set; }
    public int? MusicTrackNumber { get; set; }
    public bool? MusicRepeat { get; set; }

    /// <summary>
    /// Translation name (language name) for display purposes.
    /// This is populated during bootstrap from language dictionary.
    /// Not persisted to database.
    /// </summary>
    public string? TranslationName { get; set; }

    /// <summary>
    /// Book name for display purposes.
    /// This is populated during bootstrap from Bible book service.
    /// Not persisted to database.
    /// </summary>
    public string? BookName { get; set; }

    /// <summary>
    /// Compare by schedule ID for ObservableHashSet ordering.
    /// </summary>
    public int CompareTo(object? obj)
    {
        if (obj is not ScheduleStateItem other)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }
}
