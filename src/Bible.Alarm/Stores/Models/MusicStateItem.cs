#nullable enable
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Music item DTO for Fluxor state.
/// Contains all music properties needed for display and state management.
/// No database entities - this is a pure DTO.
/// </summary>
public class MusicStateItem : IComparable
{
    public int Id { get; set; }
    public MusicType MusicType { get; set; }
    public string PublicationCode { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public int TrackNumber { get; set; }
    public bool Repeat { get; set; }
    public int AlarmScheduleId { get; set; }

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

