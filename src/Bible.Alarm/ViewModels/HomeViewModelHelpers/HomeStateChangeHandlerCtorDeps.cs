#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.ViewModels;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

public sealed record HomeStateChangeHandlerDeps(
    ILogger Logger,
    ScheduleDataPreparer DataPreparer,
    ScheduleViewModelManager ViewModelManager);

public sealed record HomeStateChangeHandlerCallbacks(
    Action<bool> SetIsBusy,
    Func<bool> GetIsBusy,
    Func<ObservableHashSet<ScheduleListItemViewModel>?> GetSchedules,
    Action<ObservableHashSet<ScheduleListItemViewModel>> SetSchedules,
    Action? NotifySchedulesChanged,
    Action UpdateProgressBarVisibility,
    Func<Task> FadeOutProgressBarAsync,
    Func<bool> IsPlaybackModalVisible);
