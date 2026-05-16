#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaybackNavigationPreviousRequestTests
{
    [Fact]
    public async Task PlayCurrentTrackAsync_and_MarkCurrentTrackAsPlayedAsync_invoke_captured_delegates()
    {
        var visited = new HashSet<int>();
        bool? playFromBeginning = null;
        var markCalls = new List<int>();

        var sut = new PlaybackNavigationPreviousRequest(
            Playlist: new List<AudioPlayerTrack>(),
            GetCurrentTrackIndex: () => 0,
            SetCurrentTrackIndex: _ => { },
            CurrentScheduleId: 9,
            IsIndefinitePlayback: false,
            TryPrependPreviousTrackAsync: () => Task.FromResult(true),
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
            HandlePlaybackFailureAsync: () => Task.CompletedTask);

        visited.Add(55);
        await sut.PlayCurrentTrackAsync(true);
        Assert.True(playFromBeginning);

        await sut.MarkCurrentTrackAsPlayedAsync(42);
        Assert.Equal(42, Assert.Single(markCalls));

        Assert.Contains(55, visited);
    }
}
