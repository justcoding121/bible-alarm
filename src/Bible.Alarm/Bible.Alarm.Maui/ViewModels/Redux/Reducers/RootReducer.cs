using Bible.Alarm.ViewModels.Redux.Actions;
using Bible.Alarm.ViewModels.Redux.Actions.Bible;
using Bible.Alarm.ViewModels.Redux.Actions.Music;
using Redux;

namespace Bible.Alarm.ViewModels.Redux.Reducers
{
    public static partial class RootReducer
    {
        public static ApplicationState Execute(ApplicationState previousState, IAction action)
        {
            if (action is InitializeAction)
            {
                return new ApplicationState()
                {
                    Schedules = (action as InitializeAction).ScheduleList
                };
            }

            if (action is AddScheduleAction)
            {
                var @params = (action as AddScheduleAction);
                previousState.Schedules.Add(@params.ScheduleListItem);
                return previousState;
            }

            if (action is RemoveScheduleAction)
            {
                var item = (action as RemoveScheduleAction).ScheduleListItem;
                previousState.Schedules.Remove(item);
                item.Dispose();
                return previousState;
            }

            if (action is UpdateScheduleAction)
            {
                return previousState;
            }

            if (action is ViewScheduleAction)
            {
                var @params = (action as ViewScheduleAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = @params.SelectedScheduleListItem
                };
            }

            if (action is BackAction)
            {
                (action as BackAction).CurrentViewModel.Dispose();
                return previousState;
            }

            if (action is MusicSelectionAction)
            {
                var @params = (action as MusicSelectionAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentMusic = @params.CurrentMusic
                };
            }

            if (action is SongBookSelectionAction)
            {
                var @params = (action as SongBookSelectionAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentMusic = previousState.CurrentMusic,
                    TentativeMusic = @params.TentativeMusic
                };
            }

            if (action is TrackSelectionAction)
            {
                var @params = (action as TrackSelectionAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentMusic = previousState.CurrentMusic,
                    TentativeMusic = @params.TentativeMusic
                };
            }

            if (action is TrackSelectedAction)
            {
                var @params = (action as TrackSelectedAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentMusic = @params.CurrentMusic,
                    TentativeMusic = previousState.TentativeMusic
                };
            }

            if (action is BibleSelectionAction)
            {
                var @params = (action as BibleSelectionAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentBibleReadingSchedule = @params.CurrentBibleReadingSchedule,
                    TentativeBibleReadingSchedule = @params.TentativeBibleReadingSchedule
                };
            }

            if (action is BookSelectionAction)
            {
                var @params = (action as BookSelectionAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                    TentativeBibleReadingSchedule = @params.TentativeBibleReadingSchedule
                };
            }

            if (action is ChapterSelectionAction)
            {
                var @params = (action as ChapterSelectionAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentBibleReadingSchedule = previousState.CurrentBibleReadingSchedule,
                    TentativeBibleReadingSchedule = @params.TentativeBibleReadingSchedule
                };
            }

            if (action is ChapterSelectedAction)
            {
                var @params = (action as ChapterSelectedAction);
                return new ApplicationState()
                {
                    Schedules = previousState.Schedules,
                    CurrentScheduleListItem = previousState.CurrentScheduleListItem,
                    CurrentBibleReadingSchedule = @params.CurrentBibleReadingSchedule,
                    TentativeBibleReadingSchedule = previousState.TentativeBibleReadingSchedule
                };
            }

            return previousState;
        }
    }
}
