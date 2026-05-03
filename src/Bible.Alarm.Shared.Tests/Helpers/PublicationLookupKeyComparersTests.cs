#nullable enable
using System.Collections.Generic;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLookupKeyComparersTests
{
    [Fact]
    public void LanguagePublication_TreatsLanguageAndPublicationAsOrdinalIgnoreCase()
    {
        var c = PublicationLookupKeyComparers.LanguagePublication.Instance;
        var a = ("E", "nw");
        var b = ("e", "NW");
        Assert.True(c.Equals(a, b));
        Assert.Equal(c.GetHashCode(a), c.GetHashCode(b));

        var set = new HashSet<(string LanguageCode, string PublicationCode)>(c);
        Assert.True(set.Add(a));
        Assert.False(set.Add(b));
        Assert.False(c.Equals(("E", "aw"), ("E", "nw")));
    }

    [Fact]
    public void LanguagePublicationSection_AllThreePartsOrdinalIgnoreCase()
    {
        var c = PublicationLookupKeyComparers.LanguagePublicationSection.Instance;
        Assert.True(c.Equals(("E", "nw", "song-1"), ("e", "NW", "SONG-1")));
        var set = new HashSet<(string, string, string)>(c);
        Assert.True(set.Add(("E", "nw", "song-1")));
        Assert.False(set.Add(("e", "NW", "SONG-1")));
    }

    [Fact]
    public void LanguagePublicationNullableSectionTrack_SectionEmptyWhenNull_TracksOrdinal()
    {
        var c = PublicationLookupKeyComparers.LanguagePublicationNullableSectionTrack.Instance;
        Assert.True(c.Equals(("E", "nw", null, "Jw"), ("e", "NW", "", "Jw")));
        Assert.False(c.Equals(("E", "nw", "", "Jw"), ("E", "nw", "", "jW")));

        Assert.True(setEqualsHash(c, ("en", "x", "", "01"), ("EN", "X", null, "01")));
    }

    [Fact]
    public void LanguagePublicationNullableSectionTrack_Tracks_UseOrdinal_ComparesNullAgainstEmptyDistinctly()
    {
        var c = PublicationLookupKeyComparers.LanguagePublicationNullableSectionTrack.Instance;
        Assert.False(c.Equals(("E", "nw", null, null!), ("e", "NW", "", "")));
        Assert.True(c.Equals(("E", "nw", null, null!), ("e", "NW", null, null!)));
        Assert.True(setEqualsHash(c, ("E", "nw", null, null!), ("e", "nw", "", null!)));
    }

    [Fact]
    public void PublicationSection_TwoPartsOrdinalIgnoreCase()
    {
        var c = PublicationLookupKeyComparers.PublicationSection.Instance;
        var set = new HashSet<(string, string)>(c);
        Assert.True(set.Add(("nw", "1")));
        Assert.False(set.Add(("NW", "1")));
    }

    [Fact]
    public void PublicationNullableSectionTrack_SectionEmptyWhenNull_TracksOrdinal()
    {
        var c = PublicationLookupKeyComparers.PublicationNullableSectionTrack.Instance;
        Assert.True(c.Equals(("nw", null, "A"), ("NW", "", "A")));
        Assert.False(c.Equals(("nw", "", "a"), ("nw", "", "A")));

        Assert.True(setEqualsHash(c, ("nw", "", "t"), ("NW", null, "t")));
    }

    [Fact]
    public void PublicationNullableSectionTrack_Tracks_UseOrdinal_ComparesNullAgainstEmptyDistinctly()
    {
        var c = PublicationLookupKeyComparers.PublicationNullableSectionTrack.Instance;
        Assert.False(c.Equals(("nw", null, null!), ("NW", "", "")));
        Assert.True(c.Equals(("nw", null, null!), ("NW", null, null!)));
        Assert.True(setEqualsHash(c, ("mel", "", null!), ("MEL", null, null!)));
    }

    [Fact]
    public void PublicationNullableLanguageCode_LanguageNullMatchesEmpty_BothSides()
    {
        var c = PublicationLookupKeyComparers.PublicationNullableLanguageCode.Instance;
        Assert.True(c.Equals(("Nw", null), ("nw", "")));
        var set = new HashSet<(string PublicationCode, string? LanguageCode)>(c);
        Assert.True(set.Add(("mel", null)));
        Assert.False(set.Add(("MEL", "")));
        Assert.False(c.Equals(("nw", ""), ("aw", "")));
    }

    [Fact]
    public void PublicationLanguage_BothOrdinalIgnoreCase()
    {
        var c = PublicationLookupKeyComparers.PublicationLanguage.Instance;
        Assert.True(setEqualsHash(c, ("nw", "E"), ("NW", "e")));
        Assert.False(c.Equals(("nw", "E"), ("aw", "E")));
    }

    /// <summary>Equal tuples must collapse to identical hash codes for dictionaries/sets.</summary>
    private static bool setEqualsHash<T>(IEqualityComparer<T> comparer, T x, T y)
        where T : notnull
    {
        if (!comparer.Equals(x, y))
        {
            return false;
        }

        var set = new HashSet<T>(comparer) { x };
        return comparer.GetHashCode(x) == comparer.GetHashCode(y) && !set.Add(y);
    }
}
