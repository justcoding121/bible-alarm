#nullable enable
using Bible.Alarm.Shared.DataStructures;


#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Reducers.Services;

/// <summary>
/// Factory for creating ApplicationState instances.
/// Separated from ApplicationReducer for better modularity.
/// </summary>
public static class StateFactory
{
    /// <summary>
    /// Creates updated state with only CurrentSchedule (single source of truth).
    /// Removed separate CurrentMusic and CurrentBiblePublicationSchedule - all data is in CurrentSchedule.
    /// </summary>
    public static ApplicationState CreateUpdatedState(
        ApplicationState state,
        ScheduleStateItem? updatedCurrentSchedule)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: updatedCurrentSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: state.ContainerReadiness,
            pendingScheduleLoad: state.PendingScheduleLoad);
    }

    public static ApplicationState CreateStateWithSchedules(
        ApplicationState state,
        ObservableHashSet<ScheduleStateItem> newSchedules,
        ScheduleStateItem? updatedCurrentSchedule = null)
    {
        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: updatedCurrentSchedule ?? state.CurrentSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: state.ContainerReadiness,
            pendingScheduleLoad: state.PendingScheduleLoad);
    }
}

