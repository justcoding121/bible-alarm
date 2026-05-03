#nullable enable

using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Shared.Tests;

public sealed class MusicTrackModelTests
{
    private static MusicTrack T(string? code) =>
        new()
        {
            TrackCode = code,
            Title = string.Empty,
            Url = string.Empty,
            LookUpPath = string.Empty,
        };

    [Fact]
    public void CompareTo_Object_WhenNotMusicTrack_ReturnsGreater()
        => Assert.Equal(1, T("1").CompareTo(new object()));

    [Fact]
    public void CompareTo_Nulls_And_Ordering_Uses_CodeComparisonSemantics()
    {
        Assert.Equal(1, T("x").CompareTo(null));

        var two = T("2");
        var ten = T("10");
        Assert.True(two.CompareTo(ten) < 0);
        Assert.True(two < ten);

        var a = T("Jw");
        var b = T("jW");
        Assert.Equal(0, a.CompareTo(b));
        Assert.False(a < b);

        Assert.True(T("baa").CompareTo(T("zzz")) < 0);
    }

    [Fact]
    public void Equals_Follows_Compare_ToZero_WithSameNormalizedNumericCodes()
    {
        Assert.True(T("01").Equals(T("001")));
        Assert.True(T("01") == T("001"));
    }

    [Fact]
    public void GetHashCode_Matches_StringOrdinal_WhenTrackCodeUsesSameConcreteStringInstance()
    {
        const string shared = "110";
        var left = new MusicTrack { TrackCode = shared, Title = "", Url = "", LookUpPath = "" };
        var right = new MusicTrack { TrackCode = shared, Title = "", Url = "", LookUpPath = "" };

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WhenTrackCodeNull_UsesDistinctInstanceIdentityPerObject()
    {
        var left = T(null);
        var right = T(null);

        Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Operator_NotEquals_DisjoinsUnequalTracks()
        => Assert.True(T("1") != T("2"));

    [Fact]
    public void ComparisonOperators_CoverGtGteLtLte_WithNumericCodes()
    {
        Assert.True(T("10") > T("2"));
        Assert.True(T("10") >= T("2"));
        Assert.True(T("2") <= T("10"));
        Assert.False(T("2") <= T(""));
    }

    [Fact]
    public void ComparisonOperators_FalseWhenEitherSideNull()
    {
        MusicTrack? nul = null;
        Assert.False(T("1") < nul);
        Assert.False(nul < T("1"));
        Assert.False(T("2") <= nul);
    }

    [Fact]
    public void Equals_OverObject_NullsAndForeignTypes_ReturnFalse()
    {
        Assert.False(T("z").Equals((object?)null));
        Assert.False(T("z").Equals("strings-are-not-music-tracks"));
    }
}
