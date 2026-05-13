#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackTests
{
    [Fact]
    public void CompareTo_orders_track_codes_numerically_when_both_parse_as_integers()
    {
        var ten = new BiblePublicationTrack { TrackCode = "10" };
        var two = new BiblePublicationTrack { TrackCode = "2" };

        Assert.True(two.CompareTo(ten) < 0);
    }

    [Fact]
    public void CompareTo_object_returns_positive_when_argument_is_not_track()
    {
        var sut = new BiblePublicationTrack { TrackCode = "1" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }
}
