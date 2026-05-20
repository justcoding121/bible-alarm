#nullable enable

using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackBibleAlarmTests
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

    [Fact]
    public void CompareTo_returns_positive_when_other_is_null()
    {
        var sut = new MusicTrack { TrackCode = "1" };

        Assert.True(sut.CompareTo((MusicTrack?)null) > 0);
    }

    [Fact]
    public void Equals_and_operators_use_track_code_comparison()
    {
        var a = new MusicTrack { TrackCode = "3" };
        var b = new MusicTrack { TrackCode = "3" };
        var c = new MusicTrack { TrackCode = "4" };

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.True(a == b);
        Assert.True(a != c);
        Assert.True(a < c);
        Assert.True(c > a);
        Assert.True(a <= b);
        Assert.True(c >= a);
    }

    [Fact]
    public void GetHashCode_uses_track_code_when_present()
    {
        var withCode = new MusicTrack { TrackCode = "7" };

        Assert.Equal(StringComparer.Ordinal.GetHashCode("7"), withCode.GetHashCode());
    }
}
