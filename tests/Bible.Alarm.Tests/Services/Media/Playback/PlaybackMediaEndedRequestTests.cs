#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMediaEndedRequestTests
{
    [Fact]
    public async Task Struct_invokes_async_and_accessor_callbacks()
    {
        var idx = 0;
        var playCalls = 0;
        var stopCalls = 0;
        var modalCalls = 0;
        var appendCalls = 0;

        var sut = new PlaybackMediaEndedRequest(
            Playlist: null,
            GetCurrentTrackIndex: () => idx,
            SetCurrentTrackIndex: i => idx = i,
            CurrentScheduleId: 9,
            IsIndefinitePlayback: false,
            TryAppendNextTrackAsync: () =>
            {
                appendCalls++;
                return Task.FromResult(false);
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
            });

        Assert.Equal(9, sut.CurrentScheduleId);
        Assert.False(sut.IsIndefinitePlayback);

        sut.SetCurrentTrackIndex(4);
        Assert.Equal(4, idx);
        Assert.False(await sut.TryAppendNextTrackAsync());
        Assert.Equal(1, appendCalls);

        await sut.PlayCurrentTrackAsync(false);
        await sut.StopAsyncInternal(true);
        await sut.ShowPlaybackErrorInModalKeepSessionAsync("x", true);

        Assert.Equal(1, playCalls);
        Assert.Equal(1, stopCalls);
        Assert.Equal(1, modalCalls);
    }
}
