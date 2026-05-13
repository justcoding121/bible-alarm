#nullable enable

using Bible.Alarm.Platforms.Windows.Effects;
using Bible.Alarm.Platforms.Windows.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsSmtcEffectTests
{
    private sealed class RecordingSmtcService : IWindowsSmtcService
    {
        public int InitializeAsyncCallCount { get; private set; }
        public List<(bool Next, bool Prev)> ButtonUpdates { get; } = new();

        public void Dispose()
        {
        }

        public Task InitializeAsync()
        {
            InitializeAsyncCallCount++;
            return Task.CompletedTask;
        }

        public void UpdateButtonStates(bool canPlayNext, bool canPlayPrevious) =>
            ButtonUpdates.Add((canPlayNext, canPlayPrevious));
    }

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private static PlaybackState Transport(PlayStatus status, bool canPlayNext = false) =>
        new(
            new PlaybackTransportSlice(
                CurrentScheduleId: null,
                IsPreparingOrPlaying: false,
                CanPlayNext: canPlayNext,
                CanPlayPrevious: false,
                Status: status,
                IsAutoAdvancing: false,
                IsTransitioningTrack: false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    [Fact]
    public async Task HandlePlaybackStarted_calls_InitializeAsync()
    {
        var svc = new RecordingSmtcService();
        var effect = new WindowsSmtcEffect(svc, new FakePlaybackState(Transport(PlayStatus.Stopped)));

        await effect.HandlePlaybackStarted(new PlaybackStartedAction(1), new NopDispatcher());

        Assert.Equal(1, svc.InitializeAsyncCallCount);
    }

    [Fact]
    public async Task HandlePlaybackNavigationChanged_sets_next_only_when_active_and_action_allows_next()
    {
        var svc = new RecordingSmtcService();
        var effect = new WindowsSmtcEffect(
            svc,
            new FakePlaybackState(Transport(PlayStatus.Playing, canPlayNext: true)));

        await effect.HandlePlaybackNavigationChanged(
            new PlaybackNavigationChangedAction(canPlayNext: true, canPlayPrevious: false),
            new NopDispatcher());

        Assert.Single(svc.ButtonUpdates);
        Assert.Equal((true, true), (svc.ButtonUpdates[0].Next, svc.ButtonUpdates[0].Prev));
    }

    [Fact]
    public async Task HandlePlaybackNavigationChanged_disables_next_when_stopped()
    {
        var svc = new RecordingSmtcService();
        var effect = new WindowsSmtcEffect(
            svc,
            new FakePlaybackState(Transport(PlayStatus.Stopped, canPlayNext: true)));

        await effect.HandlePlaybackNavigationChanged(
            new PlaybackNavigationChangedAction(canPlayNext: true, canPlayPrevious: false),
            new NopDispatcher());

        Assert.Single(svc.ButtonUpdates);
        Assert.Equal((false, false), (svc.ButtonUpdates[0].Next, svc.ButtonUpdates[0].Prev));
    }

    [Fact]
    public async Task HandlePlaybackStatusChanged_uses_action_status_for_previous_button()
    {
        var svc = new RecordingSmtcService();
        var effect = new WindowsSmtcEffect(
            svc,
            new FakePlaybackState(Transport(PlayStatus.Playing, canPlayNext: true)));

        await effect.HandlePlaybackStatusChanged(
            new PlaybackStatusChangedAction(PlayStatus.Paused),
            new NopDispatcher());

        Assert.Single(svc.ButtonUpdates);
        Assert.Equal((true, true), (svc.ButtonUpdates[0].Next, svc.ButtonUpdates[0].Prev));
    }

    [Fact]
    public async Task HandlePlaybackStatusChanged_disables_buttons_when_stopped()
    {
        var svc = new RecordingSmtcService();
        var effect = new WindowsSmtcEffect(
            svc,
            new FakePlaybackState(Transport(PlayStatus.Playing, canPlayNext: true)));

        await effect.HandlePlaybackStatusChanged(
            new PlaybackStatusChangedAction(PlayStatus.Stopped),
            new NopDispatcher());

        Assert.Single(svc.ButtonUpdates);
        Assert.Equal((false, false), (svc.ButtonUpdates[0].Next, svc.ButtonUpdates[0].Prev));
    }
}
