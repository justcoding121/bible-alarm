#nullable enable

using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageTests
{
    [Fact]
    public void CompareTo_object_returns_positive_when_argument_is_not_Language()
    {
        var sut = new Language { LanguageCode = "E" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }

    [Fact]
    public void CompareTo_Language_orders_by_LanguageCode_ordinal()
    {
        var a = new Language { LanguageCode = "A" };
        var b = new Language { LanguageCode = "B" };

        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void Equality_uses_Id_when_both_nonzero()
    {
        var x = new Language { Id = 5, LanguageCode = "X" };
        var y = new Language { Id = 5, LanguageCode = "Y" };

        Assert.True(x.Equals(y));
    }
}
