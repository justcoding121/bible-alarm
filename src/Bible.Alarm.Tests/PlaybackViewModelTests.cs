#nullable enable

using System.Runtime.CompilerServices;
using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelTests
{
    private sealed class RecordingPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession { get; set; }

        public List<string> Calls { get; } = [];

        private void Record([CallerMemberName] string name = "") => Calls.Add(name);

        public Task PlayAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PlayPreviousAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PlayNextAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task SeekForwardAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task SeekBackwardAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task SeekToAsync(TimeSpan position)
        {
            Record();
            return Task.CompletedTask;
        }

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;
        public Task StopAsync()
        {
            Record();
            return Task.CompletedTask;
        }

        public Task StopForTeardownAsync() => Task.CompletedTask;
        public Task ResetAndRetryAsync(int scheduleId)
        {
            Record();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingSchedulePlaybackService : ISchedulePlaybackService
    {
        public List<string> Calls { get; } = [];

        public Task PlayScheduleAsync(int scheduleId)
        {
            Calls.Add($"PlayScheduleAsync({scheduleId})");
            return Task.CompletedTask;
        }

        public Task<bool> CanMoveTrackAsync(int scheduleId) => Task.FromResult(true);
    }

    private sealed class NoReviewPromptService : IReviewPromptService
    {
        public Task RecordAppOpenAsync() => Task.CompletedTask;
        public Task RecordDismissEngagementAndRequestIfEligibleAsync() => Task.CompletedTask;
    }

    private sealed class RecordingReviewPromptService : IReviewPromptService
    {
        public int DismissEngagementCalls { get; private set; }

        public Task RecordAppOpenAsync() => Task.CompletedTask;

        public Task RecordDismissEngagementAndRequestIfEligibleAsync()
        {
            DismissEngagementCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class MessengerRecipient<T> : IRecipient<T>, IDisposable
        where T : class
    {
        public List<T> Received { get; } = [];

        public MessengerRecipient() =>
            WeakReferenceMessenger.Default.Register<T>(this);

        public void Receive(T message) => Received.Add(message);

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<T>(this);
    }

    private sealed class MutablePlaybackState : IState<PlaybackState>
    {
        public MutablePlaybackState(PlaybackState initial) => Value = initial;

        public PlaybackState Value { get; set; }

        public event EventHandler? StateChanged;

        public void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeAudioPlayer : IAudioPlayer
    {
        public TimeSpan? CurrentPosition { get; set; }
        public TimeSpan Duration { get; set; }
        public PlayStatus Status { get; set; } = PlayStatus.Stopped;
        public bool IsActuallyPlayingOrPaused { get; set; }

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? MediaEnded;
        public event EventHandler<EventArgs>? MediaFailed;
#pragma warning restore CS0067

        public Task PrepareAsync(AudioPlayerTrack track) => Task.CompletedTask;
        public Task PlayAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task ResumeAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public Task ResetAsync() => Task.CompletedTask;
        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;
        public Task SetMutedAsync(bool muted) => Task.CompletedTask;
        public void NotifyTrackTransitionStarting()
        {
        }

        public Task SyncMetadataForTrackAsync(AudioPlayerTrack track) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private static PlaybackViewModel CreateSut(
        IAudioPlayer? audio = null,
        IState<PlaybackState>? playbackState = null,
        RecordingPlaybackService? playback = null,
        RecordingSchedulePlaybackService? schedulePlayback = null,
        IReviewPromptService? reviewPrompt = null,
        IMainThreadScheduler? mainThread = null)
    {
        audio ??= new FakeAudioPlayer();
        playbackState ??= new MutablePlaybackState(new PlaybackState());
        playback ??= new RecordingPlaybackService();
        schedulePlayback ??= new RecordingSchedulePlaybackService();
        reviewPrompt ??= new NoReviewPromptService();
        mainThread ??= new SyncMainThreadScheduler();
        return new PlaybackViewModel(
            new PlaybackViewModelDeps(
                TestLogging.CreateLogger(),
                playback,
                schedulePlayback,
                playbackState,
                reviewPrompt,
                audio,
                mainThread));
    }

    private static async Task ExecuteAsync(ICommand command)
    {
        if (command is IAsyncRelayCommand asyncRelay)
        {
            await asyncRelay.ExecuteAsync(null);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand");
    }

    private static async Task ExecuteSeekAsync(ICommand command, TimeSpan position)
    {
        if (command is IAsyncRelayCommand<TimeSpan> seek)
        {
            await seek.ExecuteAsync(position);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand<TimeSpan>");
    }

    private static PlaybackState PlayingWithDuration(TimeSpan duration) =>
        new(
            new PlaybackTransportSlice(1, true, true, true, PlayStatus.Playing, false, false),
            new PlaybackMediaSlice("t", null, null, null, duration, null),
            new PlaybackDefaultScheduleSlice(null, null, null, null, null));

    [Fact]
    public void Receive_BeginStopping_sets_IsStopping()
    {
        using var vm = CreateSut();
        Assert.False(vm.IsStopping);

        vm.Receive(new BeginStoppingPlaybackMessage());

        Assert.True(vm.IsStopping);
    }

    [Fact]
    public void Receive_PlaybackPosition_does_not_update_time_while_stopping()
    {
        using var vm = CreateSut();
        Assert.Equal("00:00", vm.CurrentTime);

        vm.BeginStoppingUi();
        vm.Receive(new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(45) });

        Assert.Equal("00:00", vm.CurrentTime);
    }

    [Fact]
    public void Receive_PlaybackPosition_updates_time_when_duration_known()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();

        vm.Receive(new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(30) });

        Assert.Equal("00:30", vm.CurrentTime);
    }

    [Fact]
    public void Receive_PreparationProgress_sets_preparing_when_tracks_remain()
    {
        using var vm = CreateSut();

        vm.Receive(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 0,
            TotalTracks = 2,
            TotalBytesDownloaded = 0,
            CurrentTrackProgress = 0,
            ShowPercent = true,
        });

        Assert.True(vm.IsPreparing);
        Assert.True(vm.ShowPreparationPercent);
    }

    [Fact]
    public void NotifyLandscapeInteraction_when_stopping_does_not_toggle_overlay()
    {
        using var vm = CreateSut();
        vm.SetIsLandscape(true);
        Assert.True(vm.AreLandscapeOverlayControlsVisible);

        vm.BeginStoppingUi();
        vm.NotifyLandscapeInteraction();

        Assert.True(vm.AreLandscapeOverlayControlsVisible);
    }

    [Fact]
    public void CurrentScheduleId_reflects_playback_state()
    {
        var state = new MutablePlaybackState(new PlaybackState { CurrentScheduleId = 42 });
        using var vm = CreateSut(playbackState: state);
        Assert.Equal(42, vm.CurrentScheduleId);
    }

    [Fact]
    public void IsBuffering_true_when_loading_and_not_preparing()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = new PlaybackState { Status = PlayStatus.Loading };
        state.NotifyStateChanged();

        Assert.True(vm.IsBuffering);
    }

    [Fact]
    public void ShowLandscapeOverlayControls_false_while_preparing()
    {
        using var vm = CreateSut();
        vm.SetIsLandscape(true);
        Assert.True(vm.ShowLandscapeOverlayControls);

        vm.Receive(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 0,
                TotalTracks = 2,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
            });

        Assert.True(vm.IsPreparing);
        Assert.False(vm.ShowLandscapeOverlayControls);
    }

    [Fact]
    public void HasError_true_when_playback_state_reports_error()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = new PlaybackState { ErrorMessage = "Playback failed" };
        state.NotifyStateChanged();

        Assert.True(vm.HasError);
        Assert.Equal("Playback failed", vm.ErrorMessage);
    }

    [Fact]
    public void ShowMainPlayerContent_false_when_state_has_error()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = new PlaybackState { ErrorMessage = "e", Status = PlayStatus.Playing };
        state.NotifyStateChanged();

        Assert.False(vm.ShowMainPlayerContent);
    }

    [Fact]
    public void IsShowProgressBarAnimation_true_when_transitioning_track()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = new PlaybackState
        {
            Status = PlayStatus.Paused,
            IsTransitioningTrack = true,
        };
        state.NotifyStateChanged();

        Assert.True(vm.IsShowProgressBarAnimation);
    }

    [Fact]
    public void IsBuffering_false_while_preparing_even_if_status_loading()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        vm.Receive(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 0,
                TotalTracks = 2,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
            });

        state.Value = new PlaybackState { Status = PlayStatus.Loading };
        state.NotifyStateChanged();

        Assert.False(vm.IsBuffering);
    }

    [Fact]
    public void SetIsLandscape_UpdatesLayoutFlags()
    {
        using var vm = CreateSut();

        Assert.False(vm.IsLandscape);

        vm.SetIsLandscape(true);

        Assert.True(vm.IsLandscape);
        Assert.True(vm.ShowLandscapeLayout);
        Assert.False(vm.ShowPortraitLayout);
        Assert.True(vm.AreLandscapeOverlayControlsVisible);
    }

    [Fact]
    public void SetIsLandscape_SameValue_IsNoOp()
    {
        using var vm = CreateSut();
        vm.SetIsLandscape(true);
        vm.SetIsLandscape(true);

        Assert.True(vm.IsLandscape);
    }

    [Fact]
    public void NotifyLandscapeInteraction_TogglesOverlayWhenLandscape()
    {
        using var vm = CreateSut();
        vm.SetIsLandscape(true);
        Assert.True(vm.AreLandscapeOverlayControlsVisible);

        vm.NotifyLandscapeInteraction();
        Assert.False(vm.AreLandscapeOverlayControlsVisible);

        vm.NotifyLandscapeInteraction();
        Assert.True(vm.AreLandscapeOverlayControlsVisible);
    }

    [Fact]
    public void NotifyLandscapeInteraction_WhenPortrait_DoesNothing()
    {
        using var vm = CreateSut();
        Assert.False(vm.IsLandscape);

        vm.NotifyLandscapeInteraction();

        Assert.True(vm.AreLandscapeOverlayControlsVisible);
    }

    [Fact]
    public void Constructor_SyncsProgressFromAudioPlayerWhenPositionAvailable()
    {
        var audio = new FakeAudioPlayer
        {
            CurrentPosition = TimeSpan.FromSeconds(45),
            Duration = TimeSpan.FromMinutes(2),
        };

        using var vm = CreateSut(audio);

        Assert.Equal("00:45", vm.CurrentTime);
        Assert.InRange(vm.Progress, 0.374, 0.376);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        using var vm = CreateSut();
        vm.Dispose();
        vm.Dispose();
    }

    [Fact]
    public async Task PlayCommand_invokes_playback_PlayAsync()
    {
        var playback = new RecordingPlaybackService();
        using var vm = CreateSut(playback: playback);

        await ExecuteAsync(vm.PlayCommand);

        Assert.Equal(["PlayAsync"], playback.Calls);
    }

    [Fact]
    public async Task PauseCommand_invokes_playback_PauseAsync()
    {
        var playback = new RecordingPlaybackService();
        using var vm = CreateSut(playback: playback);

        await ExecuteAsync(vm.PauseCommand);

        Assert.Equal(["PauseAsync"], playback.Calls);
    }

    [Fact]
    public async Task ForwardCommand_invokes_playback_SeekForwardAsync()
    {
        var playback = new RecordingPlaybackService();
        using var vm = CreateSut(playback: playback);

        await ExecuteAsync(vm.ForwardCommand);

        Assert.Equal(["SeekForwardAsync"], playback.Calls);
    }

    [Fact]
    public async Task BackwardCommand_invokes_playback_SeekBackwardAsync()
    {
        var playback = new RecordingPlaybackService();
        using var vm = CreateSut(playback: playback);

        await ExecuteAsync(vm.BackwardCommand);

        Assert.Equal(["SeekBackwardAsync"], playback.Calls);
    }

    [Fact]
    public async Task SeekCommand_invokes_playback_SeekToAsync()
    {
        var playback = new RecordingPlaybackService();
        using var vm = CreateSut(playback: playback);

        await ExecuteSeekAsync(vm.SeekCommand, TimeSpan.FromSeconds(12));

        Assert.Equal(["SeekToAsync"], playback.Calls);
    }

    [Fact]
    public async Task PreviousCommand_invokes_playback_PlayPreviousAsync()
    {
        var playback = new RecordingPlaybackService();
        using var vm = CreateSut(playback: playback);

        await ExecuteAsync(vm.PreviousCommand);

        Assert.Equal(["PlayPreviousAsync"], playback.Calls);
    }

    [Fact]
    public async Task NextCommand_invokes_playback_PlayNextAsync()
    {
        var playback = new RecordingPlaybackService();
        using var vm = CreateSut(playback: playback);

        await ExecuteAsync(vm.NextCommand);

        Assert.Equal(["PlayNextAsync"], playback.Calls);
    }

    [Fact]
    public async Task RetryCommand_when_not_alarm_session_invokes_ResetAndRetryAsync()
    {
        var playback = new RecordingPlaybackService { IsAlarmPlaybackSession = false };
        var schedule = new RecordingSchedulePlaybackService();
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playback: playback, playbackState: state, schedulePlayback: schedule);

        state.Value = new PlaybackState
        {
            CurrentScheduleId = 55,
            ErrorMessage = "failed",
        };
        state.NotifyStateChanged();

        await ExecuteAsync(vm.RetryCommand);

        Assert.Contains(nameof(RecordingPlaybackService.ResetAndRetryAsync), playback.Calls);
        Assert.Empty(schedule.Calls);
    }

    [Fact]
    public async Task RetryCommand_when_alarm_session_invokes_PlayScheduleAsync()
    {
        var playback = new RecordingPlaybackService { IsAlarmPlaybackSession = true };
        var schedule = new RecordingSchedulePlaybackService();
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playback: playback, playbackState: state, schedulePlayback: schedule);

        state.Value = new PlaybackState
        {
            CurrentScheduleId = 99,
            ErrorMessage = "failed",
        };
        state.NotifyStateChanged();

        await ExecuteAsync(vm.RetryCommand);

        Assert.Equal(["PlayScheduleAsync(99)"], schedule.Calls);
        Assert.DoesNotContain(nameof(RecordingPlaybackService.ResetAndRetryAsync), playback.Calls);
    }

    [Fact]
    public async Task DismissCommand_sets_IsStopping_calls_StopAsync_and_review_engagement()
    {
        var playback = new RecordingPlaybackService();
        var review = new RecordingReviewPromptService();
        using var vm = CreateSut(playback: playback, reviewPrompt: review);

        Assert.False(vm.IsStopping);

        await ExecuteAsync(vm.DismissCommand);

        Assert.True(vm.IsStopping);
        Assert.Contains(nameof(RecordingPlaybackService.StopAsync), playback.Calls);
        Assert.Equal(1, review.DismissEngagementCalls);
    }

    [Fact]
    public async Task MinimizeCommand_sets_IsMinimizing_and_sends_MinimizePlaybackMessage()
    {
        using var recipient = new MessengerRecipient<MinimizePlaybackMessage>();
        using var vm = CreateSut();

        Assert.False(vm.IsMinimizing);

        await ExecuteAsync(vm.MinimizeCommand);

        Assert.True(vm.IsMinimizing);
        Assert.Single(recipient.Received);
    }

    [Fact]
    public void Receive_PreparationProgress_when_all_tracks_loaded_clears_IsPreparing()
    {
        using var vm = CreateSut();

        vm.Receive(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 0,
                TotalTracks = 2,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
            });

        Assert.True(vm.IsPreparing);

        vm.Receive(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 2,
                TotalTracks = 2,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
            });

        Assert.False(vm.IsPreparing);
    }

    [Fact]
    public void Receive_BeginStopping_routes_through_main_thread_scheduler_when_not_on_main_thread()
    {
        using var vm = CreateSut(mainThread: new OffMainThreadSyncScheduler());
        Assert.False(vm.IsStopping);

        vm.Receive(new BeginStoppingPlaybackMessage());

        Assert.True(vm.IsStopping);
    }

    [Fact]
    public void ResetProgressUi_resets_displayed_time_when_not_slider_interacting()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();

        vm.Receive(new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(30) });
        Assert.Equal("00:30", vm.CurrentTime);

        vm.ResetProgressUi();

        Assert.Equal("00:00", vm.CurrentTime);
        Assert.Equal(0.0, vm.Progress);
    }

    [Fact]
    public void OnSliderTapped_when_playing_invokes_SeekToAsync()
    {
        var playback = new RecordingPlaybackService();
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playback: playback, playbackState: state);

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();

        vm.OnSliderTapped(0.5);

        Assert.Contains(nameof(RecordingPlaybackService.SeekToAsync), playback.Calls);
    }

    [Fact]
    public void OnSliderDragCompleted_when_playing_invokes_SeekToAsync()
    {
        var playback = new RecordingPlaybackService();
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playback: playback, playbackState: state);

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();

        vm.OnSliderDragCompleted(0.4);

        Assert.Contains(nameof(RecordingPlaybackService.SeekToAsync), playback.Calls);
    }

    [Fact]
    public void OnSliderTapped_while_stopping_does_not_seek()
    {
        var playback = new RecordingPlaybackService();
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playback: playback, playbackState: state);

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();
        vm.BeginStoppingUi();

        vm.OnSliderTapped(0.5);

        Assert.DoesNotContain(nameof(RecordingPlaybackService.SeekToAsync), playback.Calls);
    }

    [Fact]
    public void ResetProgressUi_while_slider_interacting_does_not_reset_time()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();

        vm.Receive(new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(30) });
        Assert.Equal("00:30", vm.CurrentTime);

        vm.OnSliderDragStarted();
        vm.ResetProgressUi();

        Assert.Equal("00:30", vm.CurrentTime);
    }

    [Fact]
    public void SetProgressDirectly_clamps_and_updates_Progress()
    {
        using var vm = CreateSut();

        vm.SetProgressDirectly(1.25);

        Assert.Equal(1.0, vm.Progress);

        vm.SetProgressDirectly(-0.5);

        Assert.Equal(0.0, vm.Progress);
    }

    [Fact]
    public void Receive_PreparationProgress_ShowPercent_changes_reflect_on_view_model()
    {
        using var vm = CreateSut();

        vm.Receive(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 0,
                TotalTracks = 2,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
                ShowPercent = false,
            });

        Assert.False(vm.ShowPreparationPercent);

        vm.Receive(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 0,
                TotalTracks = 2,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
                ShowPercent = true,
            });

        Assert.True(vm.ShowPreparationPercent);
    }

    [Fact]
    public void ProgressText_shows_zero_percent_while_preparing_without_byte_totals()
    {
        using var vm = CreateSut();

        vm.Receive(
            new PlaybackPreparationProgressMessage
            {
                LoadedTracks = 0,
                TotalTracks = 3,
                TotalBytesDownloaded = 0,
                CurrentTrackProgress = 0,
            });

        Assert.Equal("0%", vm.ProgressText);
    }

    [Fact]
    public void ResetProgressUi_applies_when_main_thread_scheduler_reports_background_thread()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state, mainThread: new OffMainThreadSyncScheduler());

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();

        vm.Receive(new PlaybackPositionChangedMessage { CurrentPosition = TimeSpan.FromSeconds(10) });
        Assert.Equal("00:10", vm.CurrentTime);

        vm.ResetProgressUi();

        Assert.Equal("00:00", vm.CurrentTime);
    }

    [Fact]
    public void Receive_PlaybackPosition_leaves_progress_unchanged_when_delta_below_threshold()
    {
        var state = new MutablePlaybackState(new PlaybackState());
        using var vm = CreateSut(playbackState: state);

        state.Value = PlayingWithDuration(TimeSpan.FromMinutes(2));
        state.NotifyStateChanged();

        var atHalf = TimeSpan.FromSeconds(30);
        vm.Receive(new PlaybackPositionChangedMessage { CurrentPosition = atHalf });
        var progress = vm.Progress;

        vm.Receive(
            new PlaybackPositionChangedMessage
            {
                CurrentPosition = atHalf + TimeSpan.FromMilliseconds(20),
            });

        Assert.Equal(progress, vm.Progress);
    }
}
