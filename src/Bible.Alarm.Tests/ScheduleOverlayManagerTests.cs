#nullable enable

using Bible.Alarm.Stores.Actions;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleOverlayManagerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    [Fact]
    public void HideHomePageOverlay_dispatches_hide_home_overlay()
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = new ScheduleOverlayManager(dispatcher);

        sut.HideHomePageOverlay();

        var action = Assert.Single(dispatcher.Dispatched);
        var home = Assert.IsType<SetHomePageOverlayAction>(action);
        Assert.False(home.IsVisible);
    }

    [Fact]
    public void ShowSchedulePageOverlay_dispatches_visible_schedule_overlay()
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = new ScheduleOverlayManager(dispatcher);

        sut.ShowSchedulePageOverlay();

        var action = Assert.Single(dispatcher.Dispatched);
        var overlay = Assert.IsType<SetSchedulePageOverlayAction>(action);
        Assert.True(overlay.IsVisible);
    }

    [Fact]
    public void HideSchedulePageOverlay_dispatches_hidden_schedule_overlay()
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = new ScheduleOverlayManager(dispatcher);

        sut.HideSchedulePageOverlay();

        var action = Assert.Single(dispatcher.Dispatched);
        var overlay = Assert.IsType<SetSchedulePageOverlayAction>(action);
        Assert.False(overlay.IsVisible);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetSchedulePageOverlay_passes_visibility(bool isVisible)
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = new ScheduleOverlayManager(dispatcher);

        sut.SetSchedulePageOverlay(isVisible);

        var action = Assert.Single(dispatcher.Dispatched);
        var overlay = Assert.IsType<SetSchedulePageOverlayAction>(action);
        Assert.Equal(isVisible, overlay.IsVisible);
    }

    [Fact]
    public void Dispose_does_not_dispatch()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new ScheduleOverlayManager(dispatcher);

        sut.Dispose();

        Assert.Empty(dispatcher.Dispatched);
    }
}
