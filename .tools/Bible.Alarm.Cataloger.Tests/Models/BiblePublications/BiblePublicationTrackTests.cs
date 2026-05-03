using Bible.Alarm.Cataloger.Models.BiblePublications;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class BiblePublicationTrackTests
{
    private static BiblePublicationTrack Track(string trackCode, string title = "") =>
        new()
        {
            TrackCode = trackCode,
            Title = title,
        };

    [Fact]
    public void CompareTo_orders_numeric_codes_by_value()
    {
        var low = Track("3");
        var high = Track("100");

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);

        Assert.Equal(1, low.CompareTo((BiblePublicationTrack?)null));
        Assert.True(low < high);
        Assert.False(low > high);

        Assert.Equal(0, Track("05").CompareTo(Track("5")));
    }

    [Fact]
    public void CompareTo_falls_back_to_ordinal_ignore_case_for_non_numeric_codes()
    {
        var alpha = Track("bee");
        var mixed = Track("Bee");

        Assert.Equal(0, alpha.CompareTo(mixed));
        Assert.True(Track("bee").CompareTo(Track("ce")) < 0);
        Assert.False(mixed.Equals(new object()));
    }

    [Fact]
    public void CompareTo_object_non_track_behaves_like_null_other()
    {
        Assert.Equal(1, Track("1").CompareTo(new object()));
    }

    [Fact]
    public void Equality_and_HashCode_use_CompareTo_and_TrackCode_Title_hashes()
    {
        var left = Track("Bee", title: "TiTle");
        var ignoreCaseCodes = Track("bee", title: "title");

        Assert.True(left.Equals(ignoreCaseCodes));
        Assert.True(left.Equals((object)ignoreCaseCodes));
        Assert.Equal(left.GetHashCode(), ignoreCaseCodes.GetHashCode());

        Assert.True(left <= Track("ce"));
        Assert.True(left < Track("ce"));
        Assert.True(left <= ignoreCaseCodes);
        Assert.True(left >= ignoreCaseCodes);
    }

    [Fact]
    public void Equivalent_numeric_strings_can_compare_equal_but_carry_different_HashCodes()
    {
        var a = Track("02", title: "X");
        var b = Track("2", title: "X");

        Assert.Equal(0, a.CompareTo(b));
        Assert.True(a.Equals(b));

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
