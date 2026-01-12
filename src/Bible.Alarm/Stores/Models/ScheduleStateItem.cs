#nullable enable
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Schedule item DTO for Fluxor state.
/// Contains all schedule properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public sealed class ScheduleStateItem : IComparable
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
    public int NumberOfTracksToRead { get; set; }
    public bool AlwaysPlayFromStart { get; set; }
    public PlayType CurrentPlayItem { get; set; }
    public long LatestAlarmNotificationId { get; set; }

    // Computed properties
    public int MeridianHour => Meridian == Meridian.Am ? Hour == 0 ? 12 : Hour : Hour == 12 ? 12 : Hour % 12;
    public Meridian Meridian => Hour < 12 ? Meridian.Am : Meridian.Pm;
    public string TimeText => $"{MeridianHour:D2}:{Minute:D2}";

    // Bible Reading Schedule properties (flattened)
    public int? BiblePublicationScheduleId { get; set; }

    public string? BiblePublicationLanguageCode { get; set; }
    public string? BiblePublicationCode { get; set; }

    /// <summary>
    /// Section number for traditional Bible readings (1-66).
    /// Null for drama publications which don't have sections.
    /// Use PublicationTypeHelper.HasSectionStructure() to check if this applies.
    /// </summary>
    public int? BiblePublicationSectionNumber { get; set; }

    public int? BiblePublicationTrackNumber { get; set; }
    public TimeSpan? BiblePublicationFinishedDuration { get; set; }

    // Music properties (flattened)
    public int? MusicId { get; set; }
    public MusicType? MusicType { get; set; }
    public string? MusicPublicationCode { get; set; }
    public string? MusicLanguageCode { get; set; }
    public int? MusicTrackNumber { get; set; }
    public bool? MusicRepeat { get; set; }

    /// <summary>
    /// Bible reading language name for display purposes.
    /// This is populated during bootstrap from language dictionary.
    /// Not persisted to database.
    /// </summary>
    public string? BiblePublicationLanguageName { get; set; }

    /// <summary>
    /// Bible reading language direction for RTL/LTR display.
    /// Values: "ltr" (left-to-right) or "rtl" (right-to-left).
    /// This is populated during bootstrap from language dictionary.
    /// Not persisted to database.
    /// </summary>
    public string? BiblePublicationLanguageDirection { get; set; }

    /// <summary>
    /// Bible reading publication name for display purposes.
    /// This is populated during bootstrap from Bible publication service.
    /// Not persisted to database.
    /// </summary>
    public string? BiblePublicationName { get; set; }

    /// <summary>
    /// Bible reading section name for display purposes.
    /// This is populated during bootstrap from Bible section service.
    /// Not persisted to database.
    /// </summary>
    public string? BiblePublicationSectionName { get; set; }

    /// <summary>
    /// Bible reading track title for display purposes.
    /// Used for drama/video publications that don't have sections.
    /// This is populated during bootstrap from track data.
    /// Not persisted to database.
    /// </summary>
    public string? BiblePublicationTrackTitle { get; set; }

    /// <summary>
    /// Music language name for display purposes (for vocals only).
    /// This is populated during bootstrap from language dictionary.
    /// Not persisted to database.
    /// </summary>
    public string? MusicLanguageName { get; set; }

    /// <summary>
    /// Music language direction for RTL/LTR display (for vocals only).
    /// Values: "ltr" (left-to-right) or "rtl" (right-to-left).
    /// This is populated during bootstrap from language dictionary.
    /// Not persisted to database.
    /// </summary>
    public string? MusicLanguageDirection { get; set; }

    /// <summary>
    /// Music publication name (song section name) for display purposes (for vocals only).
    /// This is populated during bootstrap from vocal music service.
    /// Not persisted to database.
    /// </summary>
    public string? MusicPublicationName { get; set; }

    /// <summary>
    /// Music track name for display purposes.
    /// This is populated during bootstrap from music service.
    /// Not persisted to database.
    /// </summary>
    public string? MusicTrackName { get; set; }

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
