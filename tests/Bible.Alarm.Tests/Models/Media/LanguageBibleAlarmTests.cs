#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class LanguageBibleAlarmTests
{
    [Fact]
    public void CompareTo_object_returns_positive_when_argument_is_not_Language()
    {
        var sut = new Language { LanguageCode = "E" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }

    [Fact]
    public void CompareTo_object_delegates_when_argument_is_Language()
    {
        var a = new Language { LanguageCode = "A" };
        var b = new Language { LanguageCode = "B" };

        Assert.True(a.CompareTo((object)b) < 0);
    }

    [Fact]
    public void CompareTo_Language_orders_by_LanguageCode_ordinal()
    {
        var a = new Language { LanguageCode = "A" };
        var b = new Language { LanguageCode = "B" };

        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void CompareTo_Language_returns_positive_when_other_is_null()
    {
        var sut = new Language { LanguageCode = "E" };

        Assert.True(sut.CompareTo((Language?)null) > 0);
    }

    [Fact]
    public void Equality_uses_Id_when_both_nonzero()
    {
        var x = new Language { Id = 5, LanguageCode = "X", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var y = new Language { Id = 5, LanguageCode = "Y", Direction = AppConstants.Media.TextDirectionRightToLeft };

        Assert.True(x.Equals(y));
        Assert.True(x == y);
    }

    [Fact]
    public void Equals_object_GetHashCode_and_operators_follow_contract()
    {
        var low = new Language { LanguageCode = "aa", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var high = new Language { LanguageCode = "bb", Direction = AppConstants.Media.TextDirectionLeftToRight };

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low.Equals((object)low));
        Assert.NotEqual(0, low.GetHashCode());
        Assert.True(low <= high);
        Assert.True(high >= low);
    }
}
