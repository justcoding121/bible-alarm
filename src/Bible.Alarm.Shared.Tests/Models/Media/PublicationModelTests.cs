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
    public void Publication_not_equal_operator_when_names_differ()
    {
        Assert.True(Pub("left") != Pub("right"));
    }

    [Fact]
    public void Publication_comparison_operators_require_non_nulloperands()
    {
        Publication? nul = null;

        Assert.False(Pub("a") < nul);
        Assert.False(nul <= Pub("b"));
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
