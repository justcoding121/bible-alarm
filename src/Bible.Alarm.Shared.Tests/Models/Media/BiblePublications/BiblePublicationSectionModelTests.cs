#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionModelTests
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

    private static BiblePublicationSection Section(
        BiblePublication pub,
        string sectionCode,
        int sectionId = 0,
        string name = "Section") =>
        new()
        {
            Id = sectionId,
            Name = name,
            SectionCode = sectionCode,
            BiblePublicationId = pub.Id,
            BiblePublication = pub,
            Tracks = [],
        };

    [Fact]
    public void CompareTo_Object_NotSameType_ReturnsGreater()
        => Assert.True(Section(DummyPub(), "1").CompareTo(new object()) > 0);

    [Fact]
    public void CompareTo_Object_Null_ReturnsGreater()
        => Assert.Equal(1, Section(DummyPub(), "1").CompareTo(null));

    [Fact]
    public void CompareTo_SectionNull_ReturnsGreater()
        => Assert.Equal(1, Section(DummyPub(), "1").CompareTo((BiblePublicationSection?)null));

    [Fact]
    public void CompareTo_Orders_SectionCodes_NaturallyIncludingNumericInterpretation()
    {
        var pub = DummyPub();
        var two = Section(pub, "2");
        var ten = Section(pub, "10");

        Assert.True(two.CompareTo(ten) < 0);
        Assert.True(two < ten);
    }

    [Fact]
    public void CompareTo_FallsBackToOrdinalIgnoreCase_ForNonNumericCodes()
    {
        var pub = DummyPub();
        var a = Section(pub, "alpha");
        var b = Section(pub, "BETA");

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b > a);
    }

    [Fact]
    public void Equals_Uses_IdWhenNonZero_OtherwiseReferenceIdentity()
    {
        var pub = DummyPub();
        var x = Section(pub, "gen", sectionId: 9);
        var y = Section(pub, "exo", sectionId: 9);

        Assert.True(x.Equals(y));

        var r = Section(pub, "1", sectionId: 0);
        var same = r;
        Assert.True(r == same);

        var u = Section(pub, "2", sectionId: 0);
        var v = Section(pub, "2", sectionId: 0);
        Assert.False(u.Equals(v));
    }

    [Fact]
    public void GetHashCode_Matches_OnSameNonZero_Id()
    {
        var pub = DummyPub();
        var a = Section(pub, "a", sectionId: 101);
        var b = Section(pub, "b", sectionId: 101);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Comparison_operators_require_non_null_operands()
    {
        BiblePublicationSection? n = null;
        var s = Section(DummyPub(), "1");
        Assert.False(s < n);
        Assert.False(n < s);
        Assert.False(s > n);
        Assert.False(n > s);
        Assert.False(s <= n);
        Assert.False(n <= s);
        Assert.False(s >= n);
        Assert.False(n >= s);
    }

    [Fact]
    public void GetHashCode_Differs_When_IdIsZero_OnDistinctInstances()
    {
        var pub = DummyPub();
        var a = Section(pub, "s", sectionId: 0);
        var b = Section(pub, "s", sectionId: 0);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
