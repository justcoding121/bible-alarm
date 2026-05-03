#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageModelTests
{
    private static Language Lang(int id = 1, string code = "E") =>
        new()
        {
            Id = id,
            LanguageCode = code,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

    [Fact]
    public void CompareTo_Object_NotLanguage_ReturnsGreater()
        => Assert.True(Lang(code: "AA").CompareTo(new object()) > 0);

    [Fact]
    public void CompareTo_Null_And_Relational_OrderByOrdinalLanguageCode()
    {
        Assert.Equal(1, Lang().CompareTo(null));

        var en = Lang(code: "EN");
        var fr = Lang(code: "FR");

        Assert.True(en.CompareTo(fr) < 0);
        Assert.True(en < fr);
        Assert.True(en <= fr);
        Assert.True(fr >= en);
        Assert.True(fr > en);
    }

    [Fact]
    public void Equals_Uses_IdWhenNonZero_OtherwiseReferenceIdentity()
    {
        var a = Lang(id: 3, code: "DE");
        var b = Lang(id: 3, code: "JP");

        Assert.True(a.Equals(b));

        var self = Lang(id: 0, code: "SGN");
        var sameRef = self;
        Assert.True(self == sameRef);

        var z1 = Lang(id: 0, code: "X");
        var z2 = Lang(id: 0, code: "X");
        Assert.False(z1.Equals(z2));
    }

    [Fact]
    public void GetHashCode_RuntimeIdentity_When_IdIsZero()
    {
        var z1 = Lang(id: 0, code: "V");
        var z2 = Lang(id: 0, code: "V");

        Assert.NotEqual(z1.GetHashCode(), z2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_Uses_stable_identifier_hash_when_id_non_zero()
    {
        var a = Lang(id: 404, code: "E");
        var b = Lang(id: 404, code: "F");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals(b));
    }
}
