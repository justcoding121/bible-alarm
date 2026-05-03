#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackModelTests
{
    private static BiblePublication DummyPub(int id = 42) =>
        new()
        {
            Id = id,
            Name = "Nw",
            PublicationCode = "nw",
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
        };

    private static BiblePublicationTrack Track(
        BiblePublication pub,
        string trackCode,
        int trackId = 0,
        string title = "T") =>
        new()
        {
            Id = trackId,
            TrackCode = trackCode,
            Title = title,
            BiblePublicationId = pub.Id,
            Publication = pub,
        };

    [Fact]
    public void CompareTo_Object_NotSameType_ReturnsGreater()
        => Assert.True(Track(DummyPub(), "1").CompareTo(new object()) > 0);

    [Fact]
    public void CompareTo_Nulls_And_NumericInterpretation_Order()
    {
        Assert.Equal(1, Track(DummyPub(), "1").CompareTo(null));

        var pub = DummyPub();
        var two = Track(pub, "2");
        var ten = Track(pub, "10");

        Assert.True(two.CompareTo(ten) < 0);
        Assert.True(two < ten);
    }

    [Fact]
    public void CompareTo_StringFallback_IsOrdinalIgnoreCase()
    {
        var pub = DummyPub();
        var a = Track(pub, "Jw");
        var b = Track(pub, "jW");

        Assert.Equal(0, a.CompareTo(b));
        Assert.False(a < b);
        Assert.False(a > b);
    }

    [Fact]
    public void Equals_Uses_IdWhenNonZero_OtherwiseReferenceIdentity()
    {
        var pub = DummyPub();
        var x = Track(pub, "1", trackId: 8, title: "A");
        var y = Track(pub, "99", trackId: 8, title: "B");

        Assert.True(x.Equals(y));

        var r = Track(pub, "3", trackId: 0);
        var same = r;
        Assert.True(r == same);

        var u = Track(pub, "4", trackId: 0);
        var v = Track(pub, "4", trackId: 0);
        Assert.False(u.Equals(v));
    }

    [Fact]
    public void GetHashCode_Differs_When_IdIsZero_OnDistinctInstances()
    {
        var pub = DummyPub();
        var a = Track(pub, "s", trackId: 0);
        var b = Track(pub, "s", trackId: 0);

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
