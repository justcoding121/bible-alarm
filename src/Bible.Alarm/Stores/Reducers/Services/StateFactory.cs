#nullable enable
using Bible;
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
    public static ApplicationState CreateUpdatedState(
        ApplicationState state,
        ScheduleStateItem? updatedCurrentSchedule,
        MusicStateItem? updatedCurrentMusic,
        BiblePublicationStateItem? updatedCurrentBiblePublicationSchedule)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: updatedCurrentSchedule,
            currentMusic: updatedCurrentMusic,
            currentBiblePublicationSchedule: updatedCurrentBiblePublicationSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: state.ContainerReadiness);
    }

    public static ApplicationState CreateStateWithSchedules(
        ApplicationState state,
        ObservableHashSet<ScheduleStateItem> newSchedules,
        ScheduleStateItem? updatedCurrentSchedule = null)
    {
        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: updatedCurrentSchedule ?? state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBiblePublicationSchedule: state.CurrentBiblePublicationSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: state.ContainerReadiness);
    }
}

