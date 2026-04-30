#nullable enable
using System;
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Schedule item DTO for Fluxor state.
/// Contains all schedule properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public sealed class ScheduleStateItem : IComparable, IComparable<ScheduleStateItem>, IEquatable<ScheduleStateItem>
{
    // Schedule properties
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int Second { get; set; }
    public WeekDays DaysOfWeek { get; set; }
    public bool NotificationEnabled { get; set; }
    public bool MusicEnabled { get; set; }
    public int SnoozeMinutes { get; set; }
    public int NumberOfTracksToPlay { get; set; }
    public bool AlwaysPlayFromStart { get; set; }
    public PlayType CurrentPlayItem { get; set; }
    public long LatestAlarmNotificationId { get; set; }

    /// <summary>
    /// UTC timestamp when this schedule was last played. Null if never played.
    /// Used for home page listing order (recently played first).
    /// </summary>
    public DateTime? LastPlayedAtUtc { get; set; }

    // Computed properties
    public int MeridianHour
    {
        get
        {
            if (Meridian == Meridian.Am)
            {
                if (Hour == 0)
                {
                    return 12;
                }

                return Hour;
            }

            if (Hour == 12)
            {
                return 12;
            }

            return Hour % 12;
        }
    }
    public Meridian Meridian
    {
        get
        {
            if (Hour < 12)
            {
                return Meridian.Am;
            }

            return Meridian.Pm;
        }
    }
    public string TimeText => $"{MeridianHour:D2}:{Minute:D2}";

    // Bible Reading Schedule properties (flattened)
    public int? BiblePublicationScheduleId { get; set; }

    public int? BiblePublicationCategoryId { get; set; }
    public string? BiblePublicationCategoryName { get; set; }
    public string? BiblePublicationLanguageCode { get; set; }
    public string? BiblePublicationCode { get; set; }

    /// <summary>
    /// Section code for publications with sections.
    /// Examples:
    /// - "1" for Bible book 1
    /// - "iam-1" for melody disc 1
    ///
    /// This is persisted to the schedule DB (BiblePublicationSchedule.SectionCode).
    /// </summary>
    public string? BiblePublicationSectionCode { get; set; }

    public string? BiblePublicationTrackCode { get; set; }
    public TimeSpan? BiblePublicationFinishedDuration { get; set; }

    // Music properties (flattened from AlarmMusic)
    public int? MusicId { get; set; }
    public string? MusicPublicationCode { get; set; }
    public string? MusicLanguageCode { get; set; }
    public string? MusicSectionCode { get; set; } // Section code for music publications with sections
    public string? MusicTrackCode { get; set; }
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
    /// Whether the selected Bible publication is a music publication (from media index).
    /// Used to show/hide the music (begin-with-music) container. Populated during display name population.
    /// Not persisted to database.
    /// </summary>
    public bool BiblePublicationIsMusic { get; set; }

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
    /// Music publication name for display purposes.
    /// This is populated during bootstrap from vocal music service.
    /// Not persisted to database.
    /// </summary>
    public string? MusicPublicationName { get; set; }

    /// <summary>
    /// Music section name for display purposes (for publications with sections).
    /// This is populated during bootstrap from music section service.
    /// Not persisted to database.
    /// </summary>
    public string? MusicSectionName { get; set; }

    /// <summary>
    /// Music track name for display purposes.
    /// This is populated during bootstrap from music service.
    /// Not persisted to database.
    /// </summary>
    public string? MusicTrackName { get; set; }

    /// <summary>
    /// Count of items expected inside the Bible Publication selection modal (discovered + downloaded).
    /// Used for showing/hiding the right-arrow on the publication row without extra DB queries.
    /// Not persisted to database.
    /// </summary>
    public int? BiblePublicationModalItemCount { get; set; }

    /// <summary>
    /// Count of items expected inside the Bible Section selection modal (discovered + downloaded).
    /// Used for showing/hiding the right-arrow on the section row without extra DB queries.
    /// Not persisted to database.
    /// </summary>
    public int? BiblePublicationSectionModalItemCount { get; set; }

    /// <summary>
    /// Count of tracks for the current Bible publication/section (discovered + downloaded).
    /// Used for showing/hiding the right-arrow on the track row without extra DB queries.
    /// Not persisted to database.
    /// </summary>
    public int? BiblePublicationTrackModalItemCount { get; set; }

    /// <summary>
    /// Count of items expected inside the Music Publication selection modal (discovered + downloaded).
    /// Used for showing/hiding the right-arrow on the song publication row without extra DB queries.
    /// Not persisted to database.
    /// </summary>
    public int? MusicPublicationModalItemCount { get; set; }

    /// <summary>
    /// Count of items expected inside the Music Section selection modal (discovered + downloaded).
    /// Used for showing/hiding the right-arrow on the music section row without extra DB queries.
    /// Not persisted to database.
    /// </summary>
    public int? MusicSectionModalItemCount { get; set; }

    /// <summary>
    /// Compare for ObservableHashSet ordering: recently played first, then by Id.
    /// Schedules never played (LastPlayedAtUtc null) sort after played ones.
    /// </summary>
    public int CompareTo(ScheduleStateItem? other)
    {
        if (other is null)
        {
            return 1;
        }

        var thisPlayed = LastPlayedAtUtc ?? DateTime.MinValue;
        var otherPlayed = other.LastPlayedAtUtc ?? DateTime.MinValue;
        var playedCompare = otherPlayed.CompareTo(thisPlayed);
        if (playedCompare != 0)
        {
            return playedCompare;
        }

        return Id.CompareTo(other.Id);
    }

    public int CompareTo(object? obj) => CompareTo(obj as ScheduleStateItem);

    public bool Equals(ScheduleStateItem? other)
    {
        if (other is null)
        {
            return false;
        }

        var thisPlayed = LastPlayedAtUtc ?? DateTime.MinValue;
        var otherPlayed = other.LastPlayedAtUtc ?? DateTime.MinValue;
        return Id == other.Id && thisPlayed == otherPlayed;
    }

    public override bool Equals(object? obj) => Equals(obj as ScheduleStateItem);

    public override int GetHashCode() => HashCode.Combine(Id, LastPlayedAtUtc ?? DateTime.MinValue);

    public static bool operator ==(ScheduleStateItem? left, ScheduleStateItem? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(ScheduleStateItem? left, ScheduleStateItem? right) => !(left == right);

    public static bool operator <(ScheduleStateItem? left, ScheduleStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(ScheduleStateItem? left, ScheduleStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(ScheduleStateItem? left, ScheduleStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(ScheduleStateItem? left, ScheduleStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
