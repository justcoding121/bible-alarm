#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class HomeStateChangeHandlerTests
{
    [Fact]
    public void Ctor_wires_deps_and_callbacks()
    {
        var deps = new HomeStateChangeHandlerDeps(
            TestLogging.CreateLogger(),
            null!,
            null!);

        var callbacks = new HomeStateChangeHandlerCallbacks(
            SetIsBusy: _ => { },
            GetIsBusy: () => false,
            GetSchedules: () => null,
            SetSchedules: _ => { },
            NotifySchedulesChanged: null,
            UpdateProgressBarVisibility: () => { },
            FadeOutProgressBarAsync: () => Task.CompletedTask,
            IsPlaybackModalVisible: () => false);

        var sut = new HomeStateChangeHandler(deps, callbacks);
        Assert.NotNull(sut);
    }
}
