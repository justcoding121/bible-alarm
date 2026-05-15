#nullable enable

using Bible.Alarm.Services.Media.Playback;

namespace Bible.Alarm.Tests;

public sealed class PlaybackServiceInjectionContextTests
{
    [Fact]
    public void Record_round_trips_collaborator_slots()
    {
        var sut = new PlaybackServiceInjectionContext(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.PreparePlaybackService);
        Assert.Null(sut.TrackCdnUrlRefresher);
    }
}
