#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationTests
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
    public void TranslatedPublication_Equals_requires_matching_LanguageId_and_Name()
    {
        var left = new TranslatedPublication { Name = "pub", LanguageId = 3 };
        var rightMatch = new TranslatedPublication { Name = "pub", LanguageId = 3 };
        var wrongLang = new TranslatedPublication { Name = "pub", LanguageId = 9 };

        Assert.True(left.Equals(rightMatch));
        Assert.False(left.Equals(wrongLang));
    }
}
