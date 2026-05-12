#nullable enable


using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class ScheduleStateManagerTests
{
    private sealed class FakeAppState(ApplicationState snapshot) : Fluxor.IState<ApplicationState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public ApplicationState Value => snapshot;
    }

    [Fact]
    public void HandleStateChanged_sets_overlay_visibility_from_state()
    {
        bool? seen = null;
        var state = new FakeAppState(new ApplicationState { IsSchedulePageOverlayVisible = true });
        var notifyCount = 0;

        ScheduleStateManager.HandleStateChanged(
            state,
            v => seen = v,
            () => notifyCount++,
            (_, _) => { });

        Assert.True(seen);
        Assert.Equal(1, notifyCount);
    }

    [Fact]
    public void HandleStateChanged_when_overlay_false_passes_false_to_callback()
    {
        bool? seen = null;
        var state = new FakeAppState(new ApplicationState { IsSchedulePageOverlayVisible = false });

        ScheduleStateManager.HandleStateChanged(
            state,
            v => seen = v,
            () => { },
            (_, _) => { });

        Assert.False(seen);
    }
}
