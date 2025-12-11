using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using AutoMapper;
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
        
        // Get AutoMapper from service provider
        var mapper = Bible.Alarm.Common.ServiceProviderManager.GetService<IMapper>();
        if (mapper == null)
        {
            Log.Error("IMapper not found in OnAddSchedule - cannot map schedule to state item");
            return state;
        }
        
        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        if (state.Schedules != null)
        {
            foreach (var scheduleItem in state.Schedules)
            {
                newSchedules.Add(scheduleItem);
            }
        }
        // Map AlarmSchedule to ScheduleStateItem using AutoMapper
        // TranslationName will be null initially (can be populated later if needed)
        var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);
        newSchedules.Add(scheduleStateItem);
        
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

        var newSchedules = new ObservableHashSet<ScheduleStateItem>();
        foreach (var scheduleItem in state.Schedules)
        {
            if (scheduleItem.Id != action.Schedule.Id)
            {
                newSchedules.Add(scheduleItem);
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

        // Get AutoMapper from service provider
        var mapper = Bible.Alarm.Common.ServiceProviderManager.GetService<IMapper>();
        if (mapper == null)
        {
            Log.Error("IMapper not found in OnUpdateSchedule - cannot map schedule to state item");
            return state;
        }

        // Update the existing collection in place to avoid creating a new collection reference
        // This prevents the entire list from reloading when only one item is updated
        var existingScheduleItem = state.Schedules.FirstOrDefault(s => s.Id == action.Schedule.Id);
        if (existingScheduleItem != null)
        {
            // Preserve TranslationName from existing item when updating
            // Map the updated schedule to ScheduleStateItem
            var updatedItem = mapper.Map<ScheduleStateItem>(action.Schedule);
            updatedItem.TranslationName = existingScheduleItem.TranslationName; // Preserve TranslationName
            
            // Only remove and re-add if the schedule data actually changed
            // Compare key properties to determine if update is needed
            if (existingScheduleItem.Name != updatedItem.Name ||
                existingScheduleItem.IsEnabled != updatedItem.IsEnabled ||
                existingScheduleItem.Hour != updatedItem.Hour ||
                existingScheduleItem.Minute != updatedItem.Minute ||
                existingScheduleItem.BibleReadingLanguageCode != updatedItem.BibleReadingLanguageCode ||
                existingScheduleItem.BibleReadingBookNumber != updatedItem.BibleReadingBookNumber ||
                existingScheduleItem.BibleReadingChapterNumber != updatedItem.BibleReadingChapterNumber)
            {
                state.Schedules.Remove(existingScheduleItem);
                state.Schedules.Add(updatedItem);
            }
            // If no changes detected, no need to update the collection
        }
        else
        {
            // Schedule not found, add it (shouldn't happen, but handle gracefully)
            // Map AlarmSchedule to ScheduleStateItem using AutoMapper
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);
            state.Schedules.Add(scheduleStateItem);
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

