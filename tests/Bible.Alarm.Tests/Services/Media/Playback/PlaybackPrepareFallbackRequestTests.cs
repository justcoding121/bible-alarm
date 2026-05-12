#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaybackPrepareFallbackRequestTests
{
    [Fact]
    public async Task Struct_holds_parameters_and_invokes_callbacks()
    {
        List<AudioPlayerTrack>? playlist = null;
        var index = 0;
        var clearCalls = 0;
        List<AudioPlayerTrack>? navPlaylist = null;
        var navIndex = -1;
        var playCalls = 0;

        var sut = new PlaybackPrepareFallbackRequest(
            ScheduleId: 7,
            KeepErrorMessage: true,
            SetPlaylist: p => playlist = p,
            SetCurrentTrackIndex: i => index = i,
            ClearManuallyVisited: () => clearCalls++,
            NotifyNavigationChanged: (p, i) =>
            {
                navPlaylist = p;
                navIndex = i;
            },
            PlayCurrentTrackAsync: _ =>
            {
                playCalls++;
                return Task.CompletedTask;
            });

        Assert.Equal(7, sut.ScheduleId);
        Assert.True(sut.KeepErrorMessage);

        sut.SetPlaylist([]);
        Assert.NotNull(playlist);

        sut.SetCurrentTrackIndex(3);
        Assert.Equal(3, index);

        sut.ClearManuallyVisited();
        Assert.Equal(1, clearCalls);

        var pl = new List<AudioPlayerTrack>();
        sut.NotifyNavigationChanged(pl, 2);
        Assert.Same(pl, navPlaylist);
        Assert.Equal(2, navIndex);

        await sut.PlayCurrentTrackAsync(false);
        Assert.Equal(1, playCalls);
    }
}
