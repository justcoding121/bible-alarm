#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaybackNavigationNextRequestTests
{
    [Fact]
    public async Task Append_stop_mark_play_and_failure_delegates_invoke()
    {
        var visited = new HashSet<int>();
        var markCalls = new List<int>();
        bool? playFromBeginning = null;
        var appendCalls = 0;
        var stopCalls = 0;
        var failureCalls = 0;

        var sut = new PlaybackNavigationNextRequest(
            Playlist: [],
            GetCurrentTrackIndex: () => 2,
            SetCurrentTrackIndex: _ => { },
            CurrentScheduleId: 42,
            IsIndefinitePlayback: false,
            TryAppendNextTrackAsync: () =>
            {
                appendCalls++;
                return Task.FromResult(false);
            },
            ManuallyVisitedTrackIndices: visited,
            MarkCurrentTrackAsPlayedAsync: id =>
            {
                markCalls.Add(id);
                return Task.CompletedTask;
            },
            PlayCurrentTrackAsync: async fromBeginning =>
            {
                playFromBeginning = fromBeginning;
                await Task.Yield();
            },
            StopPlaybackAsync: () =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            HandlePlaybackFailureAsync: () =>
            {
                failureCalls++;
                return Task.CompletedTask;
            });

        visited.Add(9);
        await sut.TryAppendNextTrackAsync();
        await sut.StopPlaybackAsync();
        await sut.MarkCurrentTrackAsPlayedAsync(100);
        await sut.PlayCurrentTrackAsync(false);

        Assert.Equal(1, appendCalls);
        Assert.Equal(1, stopCalls);
        Assert.False(playFromBeginning);
        Assert.Equal(100, Assert.Single(markCalls));
        Assert.Equal(0, failureCalls);
        Assert.Contains(9, visited);
        Assert.Equal(42, sut.CurrentScheduleId);
        Assert.False(sut.IsIndefinitePlayback);
    }
}
