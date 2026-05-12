#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaybackHandleFailureRequestTests
{
    [Fact]
    public async Task SetPlaylist_SetCurrentTrackIndex_and_PlayCurrentTrackAsync_invoke_captured_delegates()
    {
        var resetCalls = 0;
        List<AudioPlayerTrack>? seenPlaylist = null;
        int? seenIndex = null;
        bool? playArg = null;

        var sut = new PlaybackHandleFailureRequest(
            IsAlarm: false,
            CurrentScheduleId: null,
            ResetAsync: () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            },
            SetPlaylist: pl => seenPlaylist = pl,
            SetCurrentTrackIndex: i => seenIndex = i,
            PlayCurrentTrackAsync: async fromBeginning =>
            {
                playArg = fromBeginning;
                await Task.Yield();
            });

        await sut.ResetAsync();
        var list = new List<AudioPlayerTrack>();
        sut.SetPlaylist(list);
        sut.SetCurrentTrackIndex(4);
        await sut.PlayCurrentTrackAsync(true);

        Assert.Equal(1, resetCalls);
        Assert.Same(list, seenPlaylist);
        Assert.Equal(4, seenIndex);
        Assert.True(playArg);
        Assert.False(sut.IsAlarm);
        Assert.Null(sut.CurrentScheduleId);
    }
}
