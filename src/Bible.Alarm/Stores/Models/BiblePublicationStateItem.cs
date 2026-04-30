#nullable enable
using System;

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Bible reading schedule item DTO for Fluxor state.
/// Contains all Bible reading schedule properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public sealed class BiblePublicationStateItem : IComparable, IComparable<BiblePublicationStateItem>, IEquatable<BiblePublicationStateItem>
{
    public int Id { get; set; }

    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string PublicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Section code for publications with sections.
    /// Examples:
    /// - "1" for Bible book 1
    /// - "iam-1" for melody disc 1
    ///
    /// This is persisted to the schedule DB (BiblePublicationSchedule.SectionCode).
    /// </summary>
    public string? SectionCode { get; set; }

    public string TrackCode { get; set; } = string.Empty;
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
    public int CompareTo(BiblePublicationStateItem? other) =>
        other is null ? 1 : Id.CompareTo(other.Id);

    public int CompareTo(object? obj) => CompareTo(obj as BiblePublicationStateItem);

    public bool Equals(BiblePublicationStateItem? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as BiblePublicationStateItem);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(BiblePublicationStateItem? left, BiblePublicationStateItem? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(BiblePublicationStateItem? left, BiblePublicationStateItem? right) => !(left == right);

    public static bool operator <(BiblePublicationStateItem? left, BiblePublicationStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(BiblePublicationStateItem? left, BiblePublicationStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(BiblePublicationStateItem? left, BiblePublicationStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(BiblePublicationStateItem? left, BiblePublicationStateItem? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
