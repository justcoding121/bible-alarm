#nullable enable

using Bible.Alarm.Platforms.Windows.Effects;
using Bible.Alarm.Platforms.Windows.Services.UI.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsMediaToastEffectTests
{
    private sealed class RecordingWindowsNotificationService : IWindowsNotificationService
    {
        public int DismissMediaToastCallCount { get; private set; }
        public List<(string? Title, string? Subtitle)> ShowMediaToastCalls { get; } = new();

        public void DismissMediaToast() => DismissMediaToastCallCount++;

        public void ShowMediaToast(
            string? title,
            string? subtitle,
            string? body,
            string? artworkUrl,
            bool canPlayNext = false,
            bool canPlayPrevious = false,
            bool isPlaying = false) =>
            ShowMediaToastCalls.Add((title, subtitle));

        public Task ShowNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule, string title, string body) =>
            Task.CompletedTask;

        public Task RemoveAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> IsScheduledAsync(int scheduleId) => Task.FromResult(false);

        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> CanScheduleAsync() => Task.FromResult(true);
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

    private static PlaybackState StateWithStatus(PlayStatus status) =>
        new(
            new PlaybackTransportSlice(
                CurrentScheduleId: null,
                IsPreparingOrPlaying: false,
                CanPlayNext: false,
                CanPlayPrevious: false,
                Status: status,
                IsAutoAdvancing: false,
                IsTransitioningTrack: false),
            new PlaybackMediaSlice(null, null, null, null, TimeSpan.Zero, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    [Fact]
    public async Task HandlePlaybackStopped_always_requests_dismiss()
    {
        var notifications = new RecordingWindowsNotificationService();
        var effect = new WindowsMediaToastEffect(notifications, new FakePlaybackState(StateWithStatus(PlayStatus.Playing)));

        await effect.HandlePlaybackStopped(new PlaybackStoppedAction(), new NopDispatcher());

        Assert.Equal(1, notifications.DismissMediaToastCallCount);
    }

    [Fact]
    public async Task HandlePlaybackStatusChanged_dismisses_when_status_is_stopped()
    {
        var notifications = new RecordingWindowsNotificationService();
        var effect = new WindowsMediaToastEffect(notifications, new FakePlaybackState(StateWithStatus(PlayStatus.Playing)));

        await effect.HandlePlaybackStatusChanged(new PlaybackStatusChangedAction(PlayStatus.Stopped), new NopDispatcher());

        Assert.Equal(1, notifications.DismissMediaToastCallCount);
    }

    [Fact]
    public async Task HandlePlaybackStatusChanged_dismisses_when_status_is_ended()
    {
        var notifications = new RecordingWindowsNotificationService();
        var effect = new WindowsMediaToastEffect(notifications, new FakePlaybackState(StateWithStatus(PlayStatus.Playing)));

        await effect.HandlePlaybackStatusChanged(new PlaybackStatusChangedAction(PlayStatus.Ended), new NopDispatcher());

        Assert.Equal(1, notifications.DismissMediaToastCallCount);
    }
}
