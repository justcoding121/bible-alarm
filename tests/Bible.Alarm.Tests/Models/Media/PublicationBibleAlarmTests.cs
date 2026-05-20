#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class PublicationBibleAlarmTests
{
    [Fact]
    public void CompareTo_object_returns_positive_when_argument_is_not_Publication()
    {
        var sut = new Publication { Name = "a" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }

    [Fact]
    public void CompareTo_orders_by_Name()
    {
        var a = new Publication { Name = "apple" };
        var b = new Publication { Name = "banana" };

        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void Equals_and_GetHashCode_use_name_ordinal()
    {
        var left = new Publication { Name = "Pub", PublicationCode = "p1" };
        var right = new Publication { Name = "Pub", PublicationCode = "p2" };

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Operators_follow_comparison_and_equality()
    {
        var low = new Publication { Name = "A" };
        var high = new Publication { Name = "B" };

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= high);
        Assert.True(high >= low);
        Assert.True(low == new Publication { Name = "A" });
        Assert.True(low != high);
    }

    [Fact]
    public void TranslatedPublication_Equals_requires_matching_LanguageId_and_Name()
    {
        var left = new TranslatedPublication { Name = "pub", LanguageId = 3, Language = new Language { LanguageCode = "E" } };
        var rightMatch = new TranslatedPublication { Name = "pub", LanguageId = 3, Language = new Language { LanguageCode = "F" } };
        var wrongLang = new TranslatedPublication { Name = "pub", LanguageId = 9, Language = new Language { LanguageCode = "E" } };

        Assert.True(left.Equals(rightMatch));
        Assert.True(left.Equals((object)rightMatch));
        Assert.False(left.Equals(wrongLang));
        Assert.Equal(left.GetHashCode(), rightMatch.GetHashCode());
    }

    [Fact]
    public void IEquatable_TranslatedPublication_Equals_delegates_to_publication_equals()
    {
        IEquatable<TranslatedPublication> left = new TranslatedPublication
        {
            Name = "pub",
            LanguageId = 3,
            Language = new Language { LanguageCode = "E" },
        };
        var right = new TranslatedPublication { Name = "pub", LanguageId = 3, Language = new Language { LanguageCode = "E" } };

        Assert.True(left.Equals(right));
    }
}
