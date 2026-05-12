#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMediaEventAdapterCallbacksTests
{
    [Fact]
    public async Task Struct_exposes_getters_and_async_hooks()
    {
        var playlist = new List<AudioPlayerTrack>();
        var idx = 0;
        var appendCalls = 0;
        var playCalls = 0;
        var stopCalls = 0;
        var modalCalls = 0;

        var sut = new PlaybackMediaEventAdapterCallbacks(
            GetPlaylist: () => playlist,
            GetCurrentTrackIndex: () => idx,
            SetCurrentTrackIndex: i => idx = i,
            GetCurrentScheduleId: () => 3,
            GetIsIndefinitePlayback: () => true,
            TryAppendNextTrackAsync: () =>
            {
                appendCalls++;
                return Task.FromResult(true);
            },
            PlayCurrentTrackAsync: _ =>
            {
                playCalls++;
                return Task.CompletedTask;
            },
            StopAsyncInternal: _ =>
            {
                stopCalls++;
                return Task.CompletedTask;
            },
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => false,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) =>
            {
                modalCalls++;
                return Task.CompletedTask;
            },
            IsPlaybackEstablishedForTrack: _ => true);

        Assert.Same(playlist, sut.GetPlaylist());
        sut.SetCurrentTrackIndex(2);
        Assert.Equal(2, idx);
        Assert.Equal(3, sut.GetCurrentScheduleId());
        Assert.True(sut.GetIsIndefinitePlayback());
        Assert.True(await sut.TryAppendNextTrackAsync());
        Assert.Equal(1, appendCalls);

        await sut.PlayCurrentTrackAsync(false);
        await sut.StopAsyncInternal(false);
        await sut.ShowPlaybackErrorInModalKeepSessionAsync("m", false);

        Assert.Equal(1, playCalls);
        Assert.Equal(1, stopCalls);
        Assert.Equal(1, modalCalls);
        Assert.True(sut.IsPlaybackEstablishedForTrack(0));
    }
}
