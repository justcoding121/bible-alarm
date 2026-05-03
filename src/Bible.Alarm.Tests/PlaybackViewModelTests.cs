#nullable enable

using System.Runtime.CompilerServices;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelTests
{
    private sealed class RecordingPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession { get; set; }

        private void Add([CallerMemberName] string name = "") { }

        public Task PlayAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task PlayPreviousAsync() => Task.CompletedTask;
        public Task PlayNextAsync() => Task.CompletedTask;
        public Task SeekForwardAsync() => Task.CompletedTask;
        public Task SeekBackwardAsync() => Task.CompletedTask;
        public Task SeekToAsync(TimeSpan position) => Task.CompletedTask;
        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public Task StopForTeardownAsync() => Task.CompletedTask;
        public Task ResetAndRetryAsync(int scheduleId) => Task.CompletedTask;
        public void Dispose()
        {
        }
    }

    private sealed class RecordingSchedulePlaybackService : ISchedulePlaybackService
    {
        public Task PlayScheduleAsync(int scheduleId) => Task.CompletedTask;
        public Task<bool> CanMoveTrackAsync(int scheduleId) => Task.FromResult(true);
    }

    private sealed class NoReviewPromptService : IReviewPromptService
    {
        public Task RecordAppOpenAsync() => Task.CompletedTask;
        public Task RecordDismissEngagementAndRequestIfEligibleAsync() => Task.CompletedTask;
    }

    private sealed class MutablePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value { get; set; } = value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
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
        IState<PlaybackState>? playbackState = null)
    {
        audio ??= new FakeAudioPlayer();
        playbackState ??= new MutablePlaybackState(new PlaybackState { Status = PlayStatus.Stopped });
        return new PlaybackViewModel(
            new PlaybackViewModelDeps(
                TestLogging.CreateLogger(),
                new RecordingPlaybackService(),
                new RecordingSchedulePlaybackService(),
                playbackState,
                new NoReviewPromptService(),
                audio,
                new SyncMainThreadScheduler()));
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
        var vm = CreateSut();
        vm.Dispose();
        vm.Dispose();
    }
}
