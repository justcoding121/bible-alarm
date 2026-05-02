#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class AlarmViewModelAutoDisposeMonitorTests
{
    private sealed class MutablePlaybackState : IState<PlaybackState>
    {
        public MutablePlaybackState(PlaybackState value) => Value = value;

        public PlaybackState Value { get; set; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public async Task Start_WhenNotPlaying_Repeatedly_CallsDisposeAfterIdleThreshold()
    {
        var state = new MutablePlaybackState(new PlaybackState
        {
            IsPreparingOrPlaying = false,
            Status = PlayStatus.Stopped
        });

        var disposed = false;
        AlarmViewModelAutoDisposeMonitor.Start(
            state,
            () => disposed,
            () => disposed = true);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!disposed && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.True(disposed);
    }

    [Fact]
    public async Task Start_WhenPlaying_DoesNotDisposeWithinShortWindow()
    {
        var state = new MutablePlaybackState(new PlaybackState
        {
            IsPreparingOrPlaying = true,
            Status = PlayStatus.Playing
        });

        var disposed = false;
        AlarmViewModelAutoDisposeMonitor.Start(
            state,
            () => disposed,
            () => disposed = true);

        await Task.Delay(2500);

        Assert.False(disposed);
    }
}
