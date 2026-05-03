#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationModelTests
{
    private static Publication Pub(string name, string code = "c") =>
        new() { Name = name, PublicationCode = code };

    private static Language Lang(string code) =>
        new() { LanguageCode = code, Direction = AppConstants.Media.TextDirectionLeftToRight };

    [Fact]
    public void Publication_CompareTo_NonPublication_ReturnsGreater()
        => Assert.True(Pub("a").CompareTo(new object()) > 0);

    [Fact]
    public void Publication_CompareTo_ObjectNull_ReturnsGreater()
        => Assert.Equal(1, Pub("a").CompareTo((object?)null));

    [Fact]
    public void Publication_CompareTo_PublicationNull_ReturnsGreater()
        => Assert.Equal(1, Pub("a").CompareTo((Publication?)null));

    [Fact]
    public void Publication_CompareTo_BoxedPublication_DelegatesTo_NameOrdinal_StringCompareTo()
    {
        var q = Pub("ribbon");
        var r = Pub("ribbon");
        Assert.Equal(0, q.CompareTo((object)r));
        Assert.True(q <= r && q >= r);
    }

    [Fact]
    public void Publication_CompareToAndRelationalOps_OrderByName()
    {
        var first = Pub("a");
        var second = Pub("b");

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(first < second);
        Assert.True(first <= second);
        Assert.True(second > first);
        Assert.True(second >= first);
    }

    [Fact]
    public void Publication_ComparisonOperators_AllFalse_when_EitherOperandNull()
    {
        Publication? nul = null;
        var pub = Pub("q");
        Assert.False(pub < nul);
        Assert.False(nul < pub);
        Assert.False(pub <= nul);
        Assert.False(nul <= pub);
        Assert.False(pub > nul);
        Assert.False(nul > pub);
        Assert.False(pub >= nul);
        Assert.False(nul >= pub);
    }

    [Fact]
    public void Publication_CompareTo_nameTie_reflects_equals_for_relational_operators()
    {
        var x = Pub("paired");
        var y = Pub("paired");
        Assert.Equal(0, x.CompareTo(y));
        Assert.True(x <= y);
        Assert.True(x >= y);
    }

    [Fact]
    public void Publication_Equals_IsOrdinalCaseSensitive_OnName()
    {
        var left = Pub("Title");
        var right = Pub("title");

        Assert.False(left.Equals(right));
        Assert.False(left == right);

        var same = Pub("Title");
        Assert.True(left.Equals(same));
        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.True(left == same);
        Assert.False(left != same);
    }

    [Fact]
    public void Publication_Equals_and_hash_use_name_only_ignore_publication_code()
    {
        var left = Pub("SharedTitle", code: "pub-a");
        var right = Pub("SharedTitle", code: "pub-b");

        Assert.True(left.Equals((Publication?)right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Publication_not_equal_operator_when_names_differ()
    {
        Assert.True(Pub("left") != Pub("right"));
    }

    [Fact]
    public void Publication_Equals_Object_rejects_null_and_non_publication_reference()
    {
        var sut = Pub("novel");
        Assert.False(sut.Equals((object?)null));
        Assert.False(sut.Equals("novel"));
    }

    [Fact]
    public void TranslatedPublication_IEquatable_uses_structural_name_and_LanguageId_match()
    {
        var langE = Lang("E");
        var left = new TranslatedPublication
        {
            Name = "Shared",
            PublicationCode = "code-a",
            LanguageId = 50,
            Language = langE,
        };
        IEquatable<TranslatedPublication> asEquatable = left;
        var right = new TranslatedPublication
        {
            Name = "Shared",
            PublicationCode = "code-b",
            LanguageId = 50,
            Language = langE,
        };

        Assert.True(asEquatable.Equals(right));
        Assert.False(asEquatable.Equals(new TranslatedPublication
        {
            Name = "Shared",
            PublicationCode = "code-b",
            LanguageId = 51,
            Language = langE,
        }));
    }

    [Fact]
    public void TranslatedPublication_Equals_Object_ReferenceEquals_matches_same_instance()
    {
        var tp = new TranslatedPublication
        {
            Name = "N",
            PublicationCode = "p",
            LanguageId = 7,
            Language = Lang("E"),
        };

        Assert.True(tp.Equals((object)tp));
    }

    [Fact]
    public void TranslatedPublication_Equals_RequiresMatchingLanguageId_AndBaseName()
    {
        var a = new TranslatedPublication { Name = "N", PublicationCode = "p", LanguageId = 1, Language = Lang("E") };
        var b = new TranslatedPublication { Name = "N", PublicationCode = "q", LanguageId = 2, Language = Lang("F") };
        var c = new TranslatedPublication { Name = "N", PublicationCode = "q", LanguageId = 1, Language = Lang("E") };

        Assert.False(a.Equals((Publication?)b));
        Assert.True(a.Equals((Publication?)c));
        Assert.False(a.Equals((Publication?)Pub("N")));
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
