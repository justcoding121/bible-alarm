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
        
        var updatedReadiness = action.ContainerName switch
        {
            "BibleSelection" => currentReadiness with { BibleSelection = true },
            "MusicSelection" => currentReadiness with { MusicSelection = true },
            "NumberOfChapter" => currentReadiness with { NumberOfChapter = true },
            "ScheduleDetails" => currentReadiness with { ScheduleDetails = true },
            _ => currentReadiness
        };

        Logger.Debug("ContainerReadyAction: {ContainerName} ready. All ready: {AllReady} (B:{Bible}, M:{Music}, N:{Number}, S:{Schedule})",
            action.ContainerName,
            updatedReadiness.AllReady,
            updatedReadiness.BibleSelection,
            updatedReadiness.MusicSelection,
            updatedReadiness.NumberOfChapter,
            updatedReadiness.ScheduleDetails);

        // Must return NEW instance for Fluxor to detect change and fire StateChanged
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
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
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible,
            containerReadiness: ContainerReadiness.NotReady);
    }

    /// <summary>
    /// Reset container readiness when viewing a schedule (new or existing).
    /// Note: This reducer only resets ContainerReadiness. The main ViewScheduleAction
    /// handling is done by ApplicationReducer.OnViewSchedule which creates the full new state.
    /// To avoid creating duplicate states, we skip this reducer since ApplicationReducer
    /// already handles ViewScheduleAction and creates a new state with ContainerReadiness.NotReady.
    /// </summary>
    // Removed: [ReducerMethod(typeof(ViewScheduleAction))] - handled by ApplicationReducer

    /// <summary>
    /// Reset container readiness when resetting schedule state (navigating away).
    /// Note: This reducer only resets ContainerReadiness. The main ResetScheduleStateAction
    /// handling is done by ApplicationReducer.OnResetScheduleState which creates the full new state.
    /// To avoid creating duplicate states, we skip this reducer since ApplicationReducer
    /// already handles ResetScheduleStateAction and creates a new state with ContainerReadiness.NotReady.
    /// </summary>
    // Removed: [ReducerMethod(typeof(ResetScheduleStateAction))] - handled by ApplicationReducer
}

