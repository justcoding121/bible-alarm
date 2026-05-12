#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class HomeStateChangeHandlerCtorDepsTests
{
    [Fact]
    public void HomeStateChangeHandlerDeps_round_trips_dependency_slots()
    {
        var sut = new HomeStateChangeHandlerDeps(null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.ViewModelManager);
    }

    [Fact]
    public async Task HomeStateChangeHandlerCallbacks_invoke_and_allow_optional_notify()
    {
        var busy = false;
        ObservableHashSet<ScheduleListItemViewModel>? schedules = null;
        var setSchedulesCalls = 0;
        var progressCalls = 0;

        var sut = new HomeStateChangeHandlerCallbacks(
            SetIsBusy: b => busy = b,
            GetIsBusy: () => busy,
            GetSchedules: () => schedules,
            SetSchedules: _ => setSchedulesCalls++,
            NotifySchedulesChanged: null,
            UpdateProgressBarVisibility: () => progressCalls++,
            FadeOutProgressBarAsync: () => Task.CompletedTask,
            IsPlaybackModalVisible: () => true);

        sut.SetIsBusy(true);
        Assert.True(sut.GetIsBusy());
        Assert.Null(sut.GetSchedules());

        sut.SetSchedules(new ObservableHashSet<ScheduleListItemViewModel>());
        Assert.Equal(1, setSchedulesCalls);

        sut.UpdateProgressBarVisibility();
        Assert.Equal(1, progressCalls);

        Assert.Null(sut.NotifySchedulesChanged);
        await sut.FadeOutProgressBarAsync();
        Assert.True(sut.IsPlaybackModalVisible());
    }
}
