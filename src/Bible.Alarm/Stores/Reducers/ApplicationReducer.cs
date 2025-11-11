using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;

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
            tentativeBibleReadingSchedule: null);
    }

    [ReducerMethod]
    public static ApplicationState OnAddSchedule(ApplicationState state, AddScheduleAction action)
    {
        var newSchedules = new ObservableHashSet<AlarmSchedule>();
        if (state.Schedules != null)
        {
            foreach (var schedule in state.Schedules)
            {
                newSchedules.Add(schedule);
            }
        }
        newSchedules.Add(action.Schedule);
        
        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnUpdateSchedule(ApplicationState state, UpdateScheduleAction action)
    {
        if (state.Schedules == null)
            return state;

        var newSchedules = new ObservableHashSet<AlarmSchedule>();
        foreach (var schedule in state.Schedules)
        {
            if (schedule.Id == action.Schedule.Id)
            {
                newSchedules.Add(action.Schedule);
            }
            else
            {
                newSchedules.Add(schedule);
            }
        }
        
        return new ApplicationState(
            schedules: newSchedules,
            currentSchedule: state.CurrentSchedule?.Id == action.Schedule.Id ? action.Schedule : state.CurrentSchedule,
            currentMusic: state.CurrentMusic,
            tentativeMusic: state.TentativeMusic,
            currentBibleReadingSchedule: state.CurrentBibleReadingSchedule,
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnBack(ApplicationState state, BackAction action)
    {
        action.CurrentViewModel?.Dispose();
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
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: action.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: action.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: action.TentativeBibleReadingSchedule);
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
            tentativeBibleReadingSchedule: state.TentativeBibleReadingSchedule);
    }
}

