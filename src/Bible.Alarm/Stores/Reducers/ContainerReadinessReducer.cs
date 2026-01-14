#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Stores.Reducers;

/// <summary>
/// Reducers for container readiness state tracking.
/// </summary>
public static class ContainerReadinessReducer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ContainerReadinessReducer));

    [ReducerMethod]
    public static ApplicationState OnContainerReady(ApplicationState state, ContainerReadyAction action)
    {
        var currentReadiness = state.ContainerReadiness;

        // Check if container is already ready to prevent duplicate processing
        bool alreadyReady = action.ContainerName switch
        {
            "BiblePublicationSelection" => currentReadiness.BiblePublicationSelection,
            "MusicSelection" => currentReadiness.MusicSelection,
            "NumberOfTrack" => currentReadiness.NumberOfTrack,
            "ScheduleDetails" => currentReadiness.ScheduleDetails,
            "AlarmSettings" => currentReadiness.AlarmSettings,
            _ => false
        };

        if (alreadyReady)
        {
            // Container already ready - this is a duplicate action, ignore it
            Logger.Debug("ContainerReadyAction: {ContainerName} already ready, ignoring duplicate action", action.ContainerName);
            return state; // Return same state to avoid unnecessary state change
        }

        var updatedReadiness = action.ContainerName switch
        {
            "BiblePublicationSelection" => currentReadiness with { BiblePublicationSelection = true },
            "MusicSelection" => currentReadiness with { MusicSelection = true },
            "NumberOfTrack" => currentReadiness with { NumberOfTrack = true },
            "ScheduleDetails" => currentReadiness with { ScheduleDetails = true },
            "AlarmSettings" => currentReadiness with { AlarmSettings = true },
            _ => currentReadiness
        };

        Logger.Debug("ContainerReadyAction: {ContainerName} ready. All ready: {AllReady} (B:{Bible}, M:{Music}, N:{Number}, S:{Schedule}, A:{Alarm})",
            action.ContainerName,
            updatedReadiness.AllReady,
            updatedReadiness.BiblePublicationSelection,
            updatedReadiness.MusicSelection,
            updatedReadiness.NumberOfTrack,
            updatedReadiness.ScheduleDetails,
            updatedReadiness.AlarmSettings);

        // Must return NEW instance for Fluxor to detect change and fire StateChanged
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBiblePublicationSchedule: state.CurrentBiblePublicationSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: updatedReadiness);
    }

    [ReducerMethod]
    public static ApplicationState OnResetContainerReadiness(ApplicationState state, ResetContainerReadinessAction action)
    {
        Logger.Debug("ResetContainerReadinessAction: Resetting all containers to not ready");

        // Must return NEW instance for Fluxor to detect change and fire StateChanged
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBiblePublicationSchedule: state.CurrentBiblePublicationSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: ContainerReadiness.NotReady);
    }

    // Note: ViewScheduleAction and ResetScheduleStateAction are handled by ApplicationReducer
    // to avoid creating duplicate states. ApplicationReducer creates the full new state
    // with ContainerReadiness.NotReady, so no separate reducer is needed here.
}

