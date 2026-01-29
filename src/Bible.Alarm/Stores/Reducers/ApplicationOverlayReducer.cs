#nullable enable

using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;

namespace Bible.Alarm.Stores.Reducers;

public static class ApplicationOverlayReducer
{
    [ReducerMethod]
    public static ApplicationState OnSetHomePageOverlay(ApplicationState state, SetHomePageOverlayAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            isHomePageOverlayVisible: action.IsVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnSetSchedulePageOverlay(ApplicationState state, SetSchedulePageOverlayAction action)
    {
        // When showing overlay, reset container readiness to ensure fresh state
        // When hiding overlay, preserve container readiness (it may have been set to ready)
        var containerReadiness = action.IsVisible
            ? Models.ContainerReadiness.NotReady
            : state.ContainerReadiness;

        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: action.IsVisible,
            containerReadiness: containerReadiness);
    }

    [ReducerMethod]
    public static ApplicationState OnResetScheduleState(ApplicationState state, ResetScheduleStateAction action)
    {
        // Reset all schedule-related state when navigating back to home
        // This ensures only one schedule is in state at any time
        // NOTE: Keep overlay visible to prevent flash during navigation animation
        // The overlay will be hidden when the schedule page is destroyed or when a new schedule page is created
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: null,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible, // Keep current overlay state during navigation
            containerReadiness: Models.ContainerReadiness.NotReady); // Reset container readiness
    }
}

