#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;

/// <summary>
/// Decides whether NumberOfTrackContainerViewModel should reset and/or reinitialize after an ApplicationState change.
/// </summary>
internal sealed class NumberOfTrackStateChangeOrchestrator
{
    private readonly ContainerReadySignaler containerReadySignaler;

    public NumberOfTrackStateChangeOrchestrator(ContainerReadySignaler containerReadySignaler)
    {
        this.containerReadySignaler = containerReadySignaler;
    }

    public (bool ShouldReset, bool ShouldReinit) GetReinitDecision(ApplicationState stateValue, int scheduleId)
    {
        var currentSchedule = stateValue.CurrentSchedule;
        var resetAndReinit = containerReadySignaler.HasSignaledReady && !stateValue.ContainerReadiness.NumberOfTrack && currentSchedule != null;
        var initWhenScheduleSet = scheduleId == 0 && currentSchedule != null && !containerReadySignaler.HasSignaledReady;
        var scheduleChanged = currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0;

        var shouldReinit = resetAndReinit || initWhenScheduleSet || scheduleChanged;
        var shouldReset = resetAndReinit || scheduleChanged;
        return (shouldReset, shouldReinit);
    }
}
