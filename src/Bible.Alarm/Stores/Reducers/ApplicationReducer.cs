using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Stores.Reducers;

public static class ApplicationReducer
{
    [ReducerMethod]
    public static ApplicationState OnInitialize(ApplicationState state, InitializeAction action)
    {
        return new ApplicationState(
            schedules: action.ScheduleList,
            currentSchedule: null,
            currentMusic: null,
            tentativeMusic: null,
            currentBibleReadingSchedule: null,
            tentativeBibleReadingSchedule: null,
            isHomePageOverlayVisible: false,
            isSchedulePageOverlayVisible: false);
    }

    [ReducerMethod]
    public static ApplicationState OnAddSchedule(ApplicationState state, AddScheduleAction action)
    {
        Log.Information("ApplicationReducer: OnAddSchedule - ScheduleId: {ScheduleId}, Name: {Name}, ExistingSchedulesCount: {ExistingCount}",
            action.Schedule?.Id, action.Schedule?.Name, state.Schedules?.Count ?? 0);
        
        var newSchedules = new ObservableHashSet<AlarmSchedule>();
        if (state.Schedules != null)
        {
            foreach (var schedule in state.Schedules)
            {
                newSchedules.Add(schedule);
            }
        }
        newSchedules.Add(action.Schedule);
        
        Log.Information("ApplicationReducer: OnAddSchedule - NewSchedulesCount: {NewCount}", newSchedules.Count);
        
        return new ApplicationState(
            schedules: newSchedules,
            // Set CurrentSchedule to the newly added schedule
            currentSchedule: action.Schedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnRemoveSchedule(ApplicationState state, RemoveScheduleAction action)
    {
        if (state.Schedules == null)
            return state;

        var newSchedules = new ObservableHashSet<AlarmSchedule>();
        foreach (var schedule in state.Schedules)
        {
            if (schedule.Id != action.Schedule.Id)
            {
                newSchedules.Add(schedule);
            }
        }
        
        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: state.CurrentSchedule?.Id == action.Schedule.Id ? null : state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnUpdateSchedule(ApplicationState state, UpdateScheduleAction action)
    {
        if (state.Schedules == null)
            return state;

        // Update the existing collection in place to avoid creating a new collection reference
        // This prevents the entire list from reloading when only one item is updated
        var existingSchedule = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);
        if (existingSchedule != null)
        {
            // Only remove and re-add if it's actually a different object reference
            // This keeps the same collection reference, preventing full list reload
            // The HomeViewModel will update the view model in place via Initialize()
            if (!ReferenceEquals(existingSchedule, action.Schedule))
            {
                state.Schedules.Remove(existingSchedule);
                state.Schedules.Add(action.Schedule);
            }
            // If it's the same reference, no need to update the collection
        }
        else
        {
            // Schedule not found, add it (shouldn't happen, but handle gracefully)
            state.Schedules.Add(action.Schedule);
        }
        
        return new ApplicationState(
            // Reuse the same collection reference
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule?.Id == action.Schedule.Id ? action.Schedule : state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnViewSchedule(ApplicationState state, ViewScheduleAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: action.SelectedSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnBack(ApplicationState state, BackAction action)
    {
        action.CurrentViewModel?.Dispose();
        // Preserve overlay state on back navigation
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnMusicSelection(ApplicationState state, MusicSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: action.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnSongBookSelection(ApplicationState state, SongBookSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: action.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnTrackSelection(ApplicationState state, TrackSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: action.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnTrackSelected(ApplicationState state, TrackSelectedAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: action.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnBibleSelection(ApplicationState state, BibleSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: action.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: action.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnBookSelection(ApplicationState state, BookSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: action.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnChapterSelection(ApplicationState state, ChapterSelectionAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: action.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnChapterSelected(ApplicationState state, ChapterSelectedAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: action.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnSetHomePageOverlay(ApplicationState state, SetHomePageOverlayAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: action.IsVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnSetSchedulePageOverlay(ApplicationState state, SetSchedulePageOverlayAction action)
    {
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: action.IsVisible);
    }

    [ReducerMethod]
    public static ApplicationState OnResetScheduleState(ApplicationState state, ResetScheduleStateAction action)
    {
        // Reset all schedule-related state when navigating back to home
        // This ensures only one schedule is in state at any time
        return new ApplicationState(
            schedules: state.Schedules,
            currentSchedule: null,
            currentMusic: null,
            tentativeMusic: null,
            currentBibleReadingSchedule: null,
            tentativeBibleReadingSchedule: null,
            isHomePageOverlayVisible: state.IsHomePageOverlayVisible,
            isSchedulePageOverlayVisible: state.IsSchedulePageOverlayVisible);
    }
}

