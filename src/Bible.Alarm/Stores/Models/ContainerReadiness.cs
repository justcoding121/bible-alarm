#nullable enable

namespace Bible.Alarm.Stores.Models;

/// <summary>
/// Tracks which schedule page containers are ready (initialized and rendered).
/// This is ephemeral state that resets when loading a new schedule.
/// </summary>
public record ContainerReadiness
{
    public bool BiblePublicationSelection { get; init; }
    public bool MusicSelection { get; init; }
    public bool NumberOfTrack { get; init; }
    public bool ScheduleDetails { get; init; }
    public bool AlarmSettings { get; init; }

    /// <summary>
    /// Returns true if all containers are ready.
    /// </summary>
    public bool AllReady => BiblePublicationSelection && MusicSelection && NumberOfTrack && ScheduleDetails && AlarmSettings;

    /// <summary>
    /// Creates a new instance with all containers not ready.
    /// </summary>
    public static ContainerReadiness NotReady => new();

    /// <summary>
    /// Creates a new instance with all containers ready.
    /// </summary>
    public static ContainerReadiness AllContainersReady => new()
    {
        BiblePublicationSelection = true,
        MusicSelection = true,
        NumberOfTrack = true,
        ScheduleDetails = true,
        AlarmSettings = true
    };
}

