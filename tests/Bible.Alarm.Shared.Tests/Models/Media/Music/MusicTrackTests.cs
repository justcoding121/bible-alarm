#nullable enable

using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class MusicTrackTests
{
    [Fact]
    public void CompareTo_orders_numeric_track_codes_naturally()
    {
        var ten = new MusicTrack { TrackCode = "10" };
        var two = new MusicTrack { TrackCode = "2" };

        Assert.True(two.CompareTo(ten) < 0);
    }

    [Fact]
    public void CompareTo_object_returns_positive_for_non_MusicTrack()
    {
        var sut = new MusicTrack { TrackCode = "1" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }
}
