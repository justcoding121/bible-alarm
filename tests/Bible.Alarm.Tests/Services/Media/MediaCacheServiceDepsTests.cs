#nullable enable

using Bible.Alarm.Services.Media;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheServiceDepsTests
{
    [Fact]
    public void Record_round_trips_dependency_slots()
    {
        var sut = new MediaCacheServiceDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.TrackCdnUrlRefresher);
    }
}
