using Bible.Alarm.Common.DataStructures;
using Bible.Alarm.Common.Redux;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Bible;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Actions.Schedule;

namespace Bible.Alarm.Stores.Reducers;

public static partial class RootReducer
{
    public static ApplicationState Execute(ApplicationState previousState, IAction action)
    {
        if (action is InitializeAction)
            return new ApplicationState
            {
                Schedules = (action as InitializeAction).ScheduleList
            };

        if (action is AddScheduleAction)
        {
            var @params = action as AddScheduleAction;
            var schedules = previousState.Schedules ?? new ObservableHashSet<AlarmSchedule>();
            var newSchedules = new ObservableHashSet<AlarmSchedule>();
            // Copy existing items
            if (schedules != null)
            {
                foreach (var schedule in schedules)
                {
                    newSchedules.Add(schedule);
                }
            }
            newSchedules.Add(@params.Schedule);
            return new ApplicationState
            {
                Schedules = newSchedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
            };
        }

        if (action is RemoveScheduleAction)
        {
            var scheduleToRemove = (action as RemoveScheduleAction).Schedule;
            if (previousState.Schedules != null)
            {
                var newSchedules = new ObservableHashSet<AlarmSchedule>();
                // Copy existing items except the one being removed (compare by ID)
                foreach (var schedule in previousState.Schedules)
                {
                    if (schedule.Id != scheduleToRemove.Id)
                    {
                        newSchedules.Add(schedule);
                    }
                }
                return new ApplicationState
                {
                    Schedules = newSchedules,
                    CurrentSchedule = previousState.CurrentSchedule?.Id == scheduleToRemove.Id ? null : previousState.CurrentSchedule,
                    CurrentMusic = previousState.CurrentMusic,
                    TentativeMusic = previousState.TentativeMusic,
                    CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                    TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
                };
            }
            return previousState;
        }

        if (action is UpdateScheduleAction)
        {
            var updatedSchedule = (action as UpdateScheduleAction).Schedule;
            if (previousState.Schedules != null)
            {
                var newSchedules = new ObservableHashSet<AlarmSchedule>();
                // Update the schedule in the collection
                foreach (var schedule in previousState.Schedules)
                {
                    if (schedule.Id == updatedSchedule.Id)
                    {
                        newSchedules.Add(updatedSchedule);
                    }
                    else
                    {
                        newSchedules.Add(schedule);
                    }
                }
                return new ApplicationState
                {
                    Schedules = newSchedules,
                    CurrentSchedule = previousState.CurrentSchedule?.Id == updatedSchedule.Id ? updatedSchedule : previousState.CurrentSchedule,
                    CurrentMusic = previousState.CurrentMusic,
                    TentativeMusic = previousState.TentativeMusic,
                    CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                    TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
                };
            }
            return previousState;
        }

        if (action is ViewScheduleAction)
        {
            var @params = action as ViewScheduleAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = @params.SelectedSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
            };
        }

        if (action is BackAction)
        {
            (action as BackAction).CurrentViewModel.Dispose();
            return previousState;
        }

        if (action is MusicSelectionAction)
        {
            var @params = action as MusicSelectionAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = @params.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
            };
        }

        if (action is SongBookSelectionAction)
        {
            var @params = action as SongBookSelectionAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = @params.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
            };
        }

        if (action is TrackSelectionAction)
        {
            var @params = action as TrackSelectionAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = @params.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
            };
        }

        if (action is TrackSelectedAction)
        {
            var @params = action as TrackSelectedAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = @params.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
            };
        }

        if (action is BibleSelectionAction)
        {
            var @params = action as BibleSelectionAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = @params.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = @params.TentativeBibleReadingSchedule
            };
        }

        if (action is BookSelectionAction)
        {
            var @params = action as BookSelectionAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = @params.TentativeBibleReadingSchedule
            };
        }

        if (action is ChapterSelectionAction)
        {
            var @params = action as ChapterSelectionAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = @params.TentativeBibleReadingSchedule
            };
        }

        if (action is ChapterSelectedAction)
        {
            var @params = action as ChapterSelectedAction;
            return new ApplicationState
            {
                Schedules = previousState.Schedules,
                CurrentSchedule = previousState.CurrentSchedule,
                CurrentMusic = previousState.CurrentMusic,
                TentativeMusic = previousState.TentativeMusic,
                CurrentBibleReadingSchedule = @params.CurrentBibleReadingSchedule,
                TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
            };
        }

        return previousState;
    }
}