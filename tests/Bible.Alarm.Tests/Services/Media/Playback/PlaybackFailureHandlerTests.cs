#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackFailureHandlerTests
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

    private sealed class StubNotificationService : INotificationService
    {
        public List<int> ShowCalls { get; } = [];
        public Exception? NextShowException { get; set; }

        public Task ShowNotificationAsync(int scheduleId)
        {
            if (NextShowException != null)
            {
                throw NextShowException;
            }

            ShowCalls.Add(scheduleId);
            return Task.CompletedTask;
        }

        public Task ScheduleNotificationAsync(Bible.Alarm.Shared.Models.Schedule.AlarmSchedule alarmSchedule, string title, string body) =>
            Task.CompletedTask;

        public Task RemoveAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> IsScheduledAsync(int scheduleId) => Task.FromResult(false);

        public Task ClearDeliveredNotificationAsync(int scheduleId) => Task.CompletedTask;

        public Task<bool> CanScheduleAsync() => Task.FromResult(true);
    }

    private sealed class StubFallbackAlarmSoundService : IFallbackAlarmSoundService
    {
        public AudioPlayerTrack? NextTrack { get; set; }
        public Exception? NextException { get; set; }

        public Task<AudioPlayerTrack?> GetFallbackAlarmTrackAsync() =>
            NextException != null
                ? Task.FromException<AudioPlayerTrack?>(NextException)
                : Task.FromResult(NextTrack);
    }

    private static TrackMetadata MinimalMeta() =>
        new()
        {
            ScheduleId = 1,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "1",
            TrackCode = "1",
            LookUpPath = "/fb",
        };

    private static AudioPlayerTrack FallbackTrack() =>
        new() { PlayItem = new PlayItem(MinimalMeta(), "https://fallback") };

    [Fact]
    public async Task HandlePlaybackFailureAsync_non_alarm_resets_and_dispatches_retry_message()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService();
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());
        var resets = 0;

        await sut.HandlePlaybackFailureAsync(new PlaybackHandleFailureRequest(
            IsAlarm: false,
            CurrentScheduleId: null,
            ResetAsync: () =>
            {
                resets++;
                return Task.CompletedTask;
            },
            SetPlaylist: _ => { },
            SetCurrentTrackIndex: _ => { },
            PlayCurrentTrackAsync: _ => Task.CompletedTask));

        Assert.Equal(1, resets);
        var error = Assert.Single(dispatcher.Dispatched.OfType<PlaybackErrorAction>());
        Assert.Equal(PlaybackUserFacingStrings.PlaybackRetryConnectionThenTapRetry, error.ErrorMessage);
        Assert.Empty(notifications.ShowCalls);
    }

    [Fact]
    public async Task HandlePlaybackFailureAsync_alarm_with_fallback_plays_and_notifies()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService { NextTrack = FallbackTrack() };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());
        List<AudioPlayerTrack>? playlist = null;
        var index = -1;
        var plays = 0;

        await sut.HandlePlaybackFailureAsync(new PlaybackHandleFailureRequest(
            IsAlarm: true,
            CurrentScheduleId: 42,
            ResetAsync: () => Task.CompletedTask,
            SetPlaylist: list => playlist = list,
            SetCurrentTrackIndex: i => index = i,
            PlayCurrentTrackAsync: _ =>
            {
                plays++;
                return Task.CompletedTask;
            }));

        Assert.Single(notifications.ShowCalls);
        Assert.Equal(42, notifications.ShowCalls[0]);
        Assert.NotNull(playlist);
        Assert.Single(playlist);
        Assert.Equal(0, index);
        Assert.Equal(1, plays);
    }

    [Fact]
    public async Task TryPlayFallbackWhenPrepareFailedAsync_dispatches_when_no_fallback_track()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService { NextTrack = null };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());

        await sut.TryPlayFallbackWhenPrepareFailedAsync(new PlaybackPrepareFallbackRequest(
            ScheduleId: 9,
            KeepErrorMessage: false,
            SetPlaylist: _ => { },
            SetCurrentTrackIndex: _ => { },
            ClearManuallyVisited: () => { },
            NotifyNavigationChanged: (_, _) => { },
            PlayCurrentTrackAsync: _ => Task.CompletedTask));

        var error = Assert.Single(dispatcher.Dispatched.OfType<PlaybackErrorAction>());
        Assert.Equal(PlaybackUserFacingStrings.DownloadFailedCheckInternet, error.ErrorMessage);
    }

    [Fact]
    public async Task TryPlayFallbackWhenPrepareFailedAsync_success_sets_playlist_and_plays()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService { NextTrack = FallbackTrack() };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());
        List<AudioPlayerTrack>? playlist = null;
        var index = -1;
        var navCalls = 0;
        var clears = 0;
        var plays = 0;

        await sut.TryPlayFallbackWhenPrepareFailedAsync(new PlaybackPrepareFallbackRequest(
            ScheduleId: 3,
            KeepErrorMessage: false,
            SetPlaylist: list => playlist = list,
            SetCurrentTrackIndex: i => index = i,
            ClearManuallyVisited: () => clears++,
            NotifyNavigationChanged: (_, _) => navCalls++,
            PlayCurrentTrackAsync: _ =>
            {
                plays++;
                return Task.CompletedTask;
            }));

        Assert.Equal(1, clears);
        Assert.Equal(1, navCalls);
        Assert.NotNull(playlist);
        Assert.Single(playlist);
        Assert.Equal(0, index);
        Assert.Equal(1, plays);
        Assert.Contains(dispatcher.Dispatched, a => a is PlaybackErrorAction { ErrorMessage: null });
    }

    [Fact]
    public async Task HandlePlaybackFailureAsync_alarm_without_fallback_dispatches_download_failed()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService { NextTrack = null };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());
        var plays = 0;

        await sut.HandlePlaybackFailureAsync(new PlaybackHandleFailureRequest(
            IsAlarm: true,
            CurrentScheduleId: 5,
            ResetAsync: () => Task.CompletedTask,
            SetPlaylist: _ => { },
            SetCurrentTrackIndex: _ => { },
            PlayCurrentTrackAsync: _ =>
            {
                plays++;
                return Task.CompletedTask;
            }));

        Assert.Single(notifications.ShowCalls);
        var errors = dispatcher.Dispatched.OfType<PlaybackErrorAction>().ToList();
        Assert.Contains(errors, e => e.ErrorMessage == PlaybackUserFacingStrings.MediaPlaybackFailedPlayingFallbackAlarm);
        Assert.Contains(errors, e => e.ErrorMessage == PlaybackUserFacingStrings.MediaDownloadFailedCheckInternet);
        Assert.Equal(0, plays);
    }

    [Fact]
    public async Task HandlePlaybackFailureAsync_alarm_notification_failure_dispatches_check_internet()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService { NextShowException = new InvalidOperationException("notify") };
        var fallback = new StubFallbackAlarmSoundService { NextTrack = FallbackTrack() };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());

        await sut.HandlePlaybackFailureAsync(new PlaybackHandleFailureRequest(
            IsAlarm: true,
            CurrentScheduleId: 8,
            ResetAsync: () => Task.CompletedTask,
            SetPlaylist: _ => { },
            SetCurrentTrackIndex: _ => { },
            PlayCurrentTrackAsync: _ => Task.CompletedTask));

        var errors = dispatcher.Dispatched.OfType<PlaybackErrorAction>().ToList();
        Assert.Contains(errors, e => e.ErrorMessage == PlaybackUserFacingStrings.MediaPlaybackFailedPlayingFallbackAlarm);
        Assert.Contains(errors, e => e.ErrorMessage == PlaybackUserFacingStrings.MediaPlaybackFailedCheckInternet);
    }

    [Fact]
    public async Task TryPlayFallbackWhenPrepareFailedAsync_keepErrorMessage_skips_clearing_error()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService { NextTrack = FallbackTrack() };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());

        await sut.TryPlayFallbackWhenPrepareFailedAsync(new PlaybackPrepareFallbackRequest(
            ScheduleId: 2,
            KeepErrorMessage: true,
            SetPlaylist: _ => { },
            SetCurrentTrackIndex: _ => { },
            ClearManuallyVisited: () => { },
            NotifyNavigationChanged: (_, _) => { },
            PlayCurrentTrackAsync: _ => Task.CompletedTask));

        Assert.DoesNotContain(dispatcher.Dispatched, a => a is PlaybackErrorAction { ErrorMessage: null });
    }

    [Fact]
    public async Task TryPlayFallbackWhenPrepareFailedAsync_play_failure_dispatches_download_failed()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService { NextTrack = FallbackTrack() };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());

        await sut.TryPlayFallbackWhenPrepareFailedAsync(new PlaybackPrepareFallbackRequest(
            ScheduleId: 4,
            KeepErrorMessage: false,
            SetPlaylist: _ => { },
            SetCurrentTrackIndex: _ => { },
            ClearManuallyVisited: () => { },
            NotifyNavigationChanged: (_, _) => { },
            PlayCurrentTrackAsync: _ => throw new InvalidOperationException("play failed")));

        var error = Assert.Single(
            dispatcher.Dispatched.OfType<PlaybackErrorAction>(),
            a => a.ErrorMessage == PlaybackUserFacingStrings.DownloadFailedCheckInternet);
        Assert.Equal(PlaybackUserFacingStrings.DownloadFailedCheckInternet, error.ErrorMessage);
    }

    [Fact]
    public async Task TryPlayFallbackWhenPrepareFailedAsync_fallback_lookup_failure_dispatches_download_failed()
    {
        var dispatcher = new RecordingDispatcher();
        var notifications = new StubNotificationService();
        var fallback = new StubFallbackAlarmSoundService
        {
            NextException = new InvalidOperationException("lookup failed"),
        };
        var sut = new PlaybackFailureHandler(fallback, notifications, dispatcher, TestLogging.CreateLogger());

        await sut.TryPlayFallbackWhenPrepareFailedAsync(new PlaybackPrepareFallbackRequest(
            ScheduleId: 6,
            KeepErrorMessage: false,
            SetPlaylist: _ => { },
            SetCurrentTrackIndex: _ => { },
            ClearManuallyVisited: () => { },
            NotifyNavigationChanged: (_, _) => { },
            PlayCurrentTrackAsync: _ => Task.CompletedTask));

        var error = Assert.Single(dispatcher.Dispatched.OfType<PlaybackErrorAction>());
        Assert.Equal(PlaybackUserFacingStrings.DownloadFailedCheckInternet, error.ErrorMessage);
    }
}
