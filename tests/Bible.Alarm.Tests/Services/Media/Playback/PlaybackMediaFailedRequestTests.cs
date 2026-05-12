#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackMediaFailedRequestTests
{
    [Fact]
    public async Task Struct_round_trips_values_and_invokes_async_play()
    {
        var playCalls = 0;
        var modalCalls = 0;

        var sut = new PlaybackMediaFailedRequest(
            Playlist: null,
            GetCurrentTrackIndex: () => 1,
            TrackUri: "u",
            TrackUrl: "url",
            PlayCurrentTrackAsync: _ =>
            {
                playCalls++;
                return Task.CompletedTask;
            },
            GetIsManualNavigationPending: () => false,
            GetIsAlarm: () => true,
            ShowPlaybackErrorInModalKeepSessionAsync: (_, _) =>
            {
                modalCalls++;
                return Task.CompletedTask;
            },
            IsPlaybackEstablishedForTrack: _ => false);

        Assert.Null(sut.Playlist);
        Assert.Equal(1, sut.GetCurrentTrackIndex());
        Assert.Equal("u", sut.TrackUri);
        Assert.True(sut.GetIsAlarm());

        await sut.PlayCurrentTrackAsync(true);
        Assert.Equal(1, playCalls);

        await sut.ShowPlaybackErrorInModalKeepSessionAsync("e", false);
        Assert.Equal(1, modalCalls);
        Assert.False(sut.IsPlaybackEstablishedForTrack(0));
    }
}
