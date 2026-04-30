namespace Bible.Alarm.Stores.Actions.Playback;

/// <summary>
/// Action to rotate the default schedule shown on Android Auto Now Playing to the next schedule.
/// Dispatched every 5 minutes by the Android Auto rotation service when car is connected and not playing.
/// </summary>
public sealed class RotateDefaultScheduleAction
{
    /// <summary>Version marker for non-empty Fluxor action type surface.</summary>
    internal const byte FluxorPayloadVersion = 1;
}
