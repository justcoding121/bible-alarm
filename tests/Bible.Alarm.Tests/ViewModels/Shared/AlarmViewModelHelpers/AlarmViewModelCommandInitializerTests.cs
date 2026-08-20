#nullable enable

using System.Runtime.CompilerServices;
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.Tests;

public sealed class AlarmViewModelCommandInitializerTests
{
    private sealed class RecordingPlaybackService : IPlaybackService
    {
        public bool IsAlarmPlaybackSession { get; set; }
        public List<string> Calls { get; } = [];
        public Func<Task>? StopAsyncBehavior { get; set; }
        public Exception? ThrowFromResetAndRetry { get; set; }

        private void Add([CallerMemberName] string name = "") => Calls.Add(name);

        public Task PlayAsync()
        {
            Add();
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            Add();
            return Task.CompletedTask;
        }

        public Task PlayPreviousAsync()
        {
            Add();
            return Task.CompletedTask;
        }

        public Task PlayNextAsync()
        {
            Add();
            return Task.CompletedTask;
        }

        public Task SeekForwardAsync()
        {
            Add();
            return Task.CompletedTask;
        }

        public Task SeekBackwardAsync()
        {
            Add();
            return Task.CompletedTask;
        }

        public Task SeekToAsync(TimeSpan position)
        {
            Calls.Add($"{nameof(SeekToAsync)}:{position}");
            return Task.CompletedTask;
        }

        public Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
        {
            Calls.Add($"{nameof(PrepareAndPlayAsync)}:{scheduleId}:{isAlarm}");
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            Add();
            if (StopAsyncBehavior != null)
            {
                await StopAsyncBehavior();
            }
        }

        public Task StopForTeardownAsync()
        {
            Add();
            return Task.CompletedTask;
        }

        public Task ResetAndRetryAsync(int scheduleId)
        {
            Calls.Add($"{nameof(ResetAndRetryAsync)}:{scheduleId}");
            return ThrowFromResetAndRetry is { } ex
                ? Task.FromException(ex)
                : Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingSchedulePlaybackService : ISchedulePlaybackService
    {
        public List<int> PlayScheduleIds { get; } = [];
        public Exception? ThrowFromPlaySchedule { get; set; }

        public Task PlayScheduleAsync(int scheduleId)
        {
            PlayScheduleIds.Add(scheduleId);
            if (ThrowFromPlaySchedule is { } ex)
            {
                throw ex;
            }

            return Task.CompletedTask;
        }

        public Task<bool> CanMoveTrackAsync(int scheduleId) =>
            Task.FromResult(true);
    }

    private static async Task ExecuteAsync(ICommand command)
    {
        if (command is IAsyncRelayCommand asyncRelay)
        {
            await asyncRelay.ExecuteAsync(null);
            return;
        }

        if (command is IRelayCommand relay)
        {
            relay.Execute(null);
            return;
        }

        throw new InvalidOperationException("Unsupported command type");
    }

    private static AlarmViewModelCommandInitializer CreateSut(
        RecordingPlaybackService playback,
        RecordingSchedulePlaybackService? schedulePlayback = null,
        Func<Task>? handleReview = null,
        Action? beginStopping = null,
        Action? resetProgress = null) =>
        new(
            TestLogging.CreateLogger(),
            playback,
            schedulePlayback ?? new RecordingSchedulePlaybackService(),
            handleReview ?? (() => Task.CompletedTask),
            beginStopping ?? (() => { }),
            resetProgress ?? (() => { }));

    [Fact]
    public async Task CreateDismissCommand_RunsStopThenReview_AfterBeginStopping()
    {
        var playback = new RecordingPlaybackService();
        var begins = 0;
        var reviews = 0;
        var sut = CreateSut(
            playback,
            handleReview: async () =>
            {
                reviews++;
                await Task.CompletedTask;
            },
            beginStopping: () => begins++);

        await ExecuteAsync(sut.CreateDismissCommand());

        Assert.Equal(1, begins);
        Assert.Contains(nameof(RecordingPlaybackService.StopAsync), playback.Calls);
        Assert.Equal(1, reviews);
    }

    [Fact]
    public async Task CreateDismissCommand_WhenStopThrows_SkipsReview()
    {
        var playback = new RecordingPlaybackService
        {
            StopAsyncBehavior = () => Task.FromException(new IOException("stop"))
        };
        var reviews = 0;
        var sut = CreateSut(
            playback,
            handleReview: async () =>
            {
                reviews++;
                await Task.CompletedTask;
            });

        await ExecuteAsync(sut.CreateDismissCommand());

        Assert.Equal(0, reviews);
    }

    [Fact]
    public async Task CreateDismissCommand_WhenReviewThrowsAfterStop_SwallowsAndLogs()
    {
        var playback = new RecordingPlaybackService();
        var reviews = 0;
        var sut = CreateSut(
            playback,
            handleReview: async () =>
            {
                reviews++;
                await Task.FromException(new IOException("review"));
            });

        await ExecuteAsync(sut.CreateDismissCommand());

        Assert.Contains(nameof(RecordingPlaybackService.StopAsync), playback.Calls);
        Assert.Equal(1, reviews);
    }

    [Fact]
    public void CreateCancelCommand_IsNoOp()
    {
        var cmd = AlarmViewModelCommandInitializer.CreateCancelCommand();
        Assert.True(cmd.CanExecute(null));
        cmd.Execute(null);
    }

    [Fact]
    public async Task CreatePlayCommand_CallsPlayAsync()
    {
        var playback = new RecordingPlaybackService();
        var sut = CreateSut(playback);

        await ExecuteAsync(sut.CreatePlayCommand());

        Assert.Equal(nameof(RecordingPlaybackService.PlayAsync), Assert.Single(playback.Calls));
    }

    [Fact]
    public async Task CreatePauseCommand_CallsPauseAsync()
    {
        var playback = new RecordingPlaybackService();
        var sut = CreateSut(playback);

        await ExecuteAsync(sut.CreatePauseCommand());

        Assert.Equal(nameof(RecordingPlaybackService.PauseAsync), Assert.Single(playback.Calls));
    }

    [Fact]
    public async Task CreatePreviousCommand_InvokesCallbacksAndPlayPrevious()
    {
        var playback = new RecordingPlaybackService();
        var resets = 0;
        var begun = 0;
        var sut = CreateSut(playback, resetProgress: () => resets++);

        await ExecuteAsync(sut.CreatePreviousCommand(() => begun++));

        Assert.Equal(1, begun);
        Assert.Equal(1, resets);
        Assert.Contains(nameof(RecordingPlaybackService.PlayPreviousAsync), playback.Calls);
    }

    [Fact]
    public async Task CreatePreviousCommand_WithoutOnBeginTrackChange_StillResetsAndSeeksPrevious()
    {
        var playback = new RecordingPlaybackService();
        var resets = 0;
        var sut = CreateSut(playback, resetProgress: () => resets++);

        await ExecuteAsync(sut.CreatePreviousCommand());

        Assert.Equal(1, resets);
        Assert.Contains(nameof(RecordingPlaybackService.PlayPreviousAsync), playback.Calls);
    }

    [Fact]
    public async Task CreateNextCommand_InvokesCallbacksAndPlayNext()
    {
        var playback = new RecordingPlaybackService();
        var resets = 0;
        var begun = 0;
        var sut = CreateSut(playback, resetProgress: () => resets++);

        await ExecuteAsync(sut.CreateNextCommand(() => begun++));

        Assert.Equal(1, begun);
        Assert.Equal(1, resets);
        Assert.Contains(nameof(RecordingPlaybackService.PlayNextAsync), playback.Calls);
    }

    [Fact]
    public async Task CreateNextCommand_WithoutOnBeginTrackChange_StillResetsAndSeeksNext()
    {
        var playback = new RecordingPlaybackService();
        var resets = 0;
        var sut = CreateSut(playback, resetProgress: () => resets++);

        await ExecuteAsync(sut.CreateNextCommand());

        Assert.Equal(1, resets);
        Assert.Contains(nameof(RecordingPlaybackService.PlayNextAsync), playback.Calls);
    }

    [Fact]
    public async Task CreateForwardCommand_CallsSeekForward()
    {
        var playback = new RecordingPlaybackService();
        var sut = CreateSut(playback);

        await ExecuteAsync(sut.CreateForwardCommand());

        Assert.Equal(nameof(RecordingPlaybackService.SeekForwardAsync), Assert.Single(playback.Calls));
    }

    [Fact]
    public async Task CreateBackwardCommand_CallsSeekBackward()
    {
        var playback = new RecordingPlaybackService();
        var sut = CreateSut(playback);

        await ExecuteAsync(sut.CreateBackwardCommand());

        Assert.Equal(nameof(RecordingPlaybackService.SeekBackwardAsync), Assert.Single(playback.Calls));
    }

    [Fact]
    public async Task CreateSeekCommand_PassesPositionToSeekTo()
    {
        var playback = new RecordingPlaybackService();
        var sut = CreateSut(playback);
        var cmd = sut.CreateSeekCommand();
        var ts = TimeSpan.FromSeconds(12);
        if (cmd is IAsyncRelayCommand<TimeSpan> asyncSeek)
        {
            await asyncSeek.ExecuteAsync(ts);
        }
        else
        {
            throw new InvalidOperationException("Expected IAsyncRelayCommand<TimeSpan>");
        }

        Assert.Equal($"SeekToAsync:{ts}", Assert.Single(playback.Calls));
    }

    [Fact]
    public void CreateRetryCommand_CanExecute_RequiresErrorAndScheduleId()
    {
        var playback = new RecordingPlaybackService();
        var sut = CreateSut(playback);
        var cmd = sut.CreateRetryCommand(() => 3, () => false, _ => { });

        Assert.False(cmd.CanExecute(null));

        cmd = sut.CreateRetryCommand(() => null, () => true, _ => { });
        Assert.False(cmd.CanExecute(null));

        cmd = sut.CreateRetryCommand(() => 4, () => true, _ => { });
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public async Task CreateRetryCommand_UsesSchedulePlayback_WhenAlarmSession()
    {
        var playback = new RecordingPlaybackService { IsAlarmPlaybackSession = true };
        var schedule = new RecordingSchedulePlaybackService();
        var sut = CreateSut(playback, schedule);
        var busy = new List<bool>();
        var cmd = sut.CreateRetryCommand(() => 99, () => true, busy.Add);

        await ExecuteAsync(cmd);

        Assert.Equal(99, Assert.Single(schedule.PlayScheduleIds));
        Assert.Empty(playback.Calls);
        Assert.Equal(new[] { true, false }, busy);
    }

    [Fact]
    public async Task CreateRetryCommand_WhenAlarmPlayScheduleThrows_StillClearsBusy()
    {
        var playback = new RecordingPlaybackService { IsAlarmPlaybackSession = true };
        var schedule = new RecordingSchedulePlaybackService { ThrowFromPlaySchedule = new IOException("schedule") };
        var sut = CreateSut(playback, schedule);
        var busy = new List<bool>();
        var cmd = sut.CreateRetryCommand(() => 22, () => true, busy.Add);

        await ExecuteAsync(cmd);

        Assert.Equal(22, Assert.Single(schedule.PlayScheduleIds));
        Assert.Equal(new[] { true, false }, busy);
    }

    [Fact]
    public async Task CreateRetryCommand_UsesResetAndRetry_WhenNotAlarmSession()
    {
        var playback = new RecordingPlaybackService { IsAlarmPlaybackSession = false };
        var schedule = new RecordingSchedulePlaybackService();
        var sut = CreateSut(playback, schedule);
        var cmd = sut.CreateRetryCommand(() => 7, () => true, _ => { });

        await ExecuteAsync(cmd);

        Assert.Equal("ResetAndRetryAsync:7", Assert.Single(playback.Calls));
        Assert.Empty(schedule.PlayScheduleIds);
    }

    [Fact]
    public async Task CreateRetryCommand_WhenResetAndRetryThrows_StillClearsBusy()
    {
        var playback = new RecordingPlaybackService
        {
            IsAlarmPlaybackSession = false,
            ThrowFromResetAndRetry = new InvalidOperationException("retry failed"),
        };
        var sut = CreateSut(playback);
        var busy = new List<bool>();
        var cmd = sut.CreateRetryCommand(() => 42, () => true, busy.Add);

        await ExecuteAsync(cmd);

        Assert.Equal("ResetAndRetryAsync:42", Assert.Single(playback.Calls));
        Assert.Equal(new[] { true, false }, busy);
    }

    [Fact]
    public async Task CreateRetryCommand_WhenScheduleIdVanishesBeforeRetry_SkipsPlayback()
    {
        var playback = new RecordingPlaybackService();
        var sut = CreateSut(playback);
        var calls = 0;
        int? GetId()
        {
            calls++;
            return calls == 1 ? 5 : null;
        }

        var busy = new List<bool>();
        var cmd = sut.CreateRetryCommand(GetId, () => true, busy.Add);

        Assert.True(cmd.CanExecute(null));
        await ExecuteAsync(cmd);

        Assert.Empty(playback.Calls);
        Assert.Equal(new[] { true, false }, busy);
    }
}
