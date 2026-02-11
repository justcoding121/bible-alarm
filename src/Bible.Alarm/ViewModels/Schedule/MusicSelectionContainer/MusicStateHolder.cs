#nullable enable
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

/// <summary>
/// Holds mutable state for MusicStateChangeHandler.
/// This allows us to avoid using ref parameters in lambdas.
/// </summary>
public sealed class MusicStateHolder
{
    public AlarmMusic? Music { get; set; }
    public AlarmMusic? LastMusic { get; set; }
    public bool MusicUpdated { get; set; }
    public bool IsUpdatingFromState { get; set; }
    public bool IsMusicEnabledNotificationQueued { get; set; }
    public bool? PendingMusicEnabled { get; set; }
}
