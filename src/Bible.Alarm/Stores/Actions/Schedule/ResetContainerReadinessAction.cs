#nullable enable

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Resets container readiness state when loading a new schedule.
/// </summary>
public sealed record ResetContainerReadinessAction
{
    /// <summary>Version marker for non-empty Fluxor action type surface.</summary>
    internal const byte FluxorPayloadVersion = 1;
}

