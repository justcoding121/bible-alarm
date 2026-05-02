using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionCodeHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Normalize_ReturnsNull_ForWhitespaceOrEmpty(string? code)
    {
        Assert.Null(SectionCodeHelper.Normalize(code));
    }

    [Fact]
    public void Normalize_Trims()
    {
        Assert.Equal("1", SectionCodeHelper.Normalize("  1 "));
    }

    [Theory]
    [InlineData("1", "01", true)]
    [InlineData("iam-1", "IAM-1", true)]
    [InlineData("1", "2", false)]
    public void CodeEquals_UsesNormalizeAndCodeComparisonRules(string? a, string? b, bool expected)
    {
        Assert.Equal(expected, SectionCodeHelper.CodeEquals(a, b));
    }

    [Fact]
    public void SectionCodeComparer_OrdersNumericSectionCodesByValue()
    {
        var cmp = SectionCodeHelper.SectionCodeComparer;
        Assert.True(cmp.Compare("2", "10") < 0);
    }

    [Fact]
    public void SectionCodeComparer_IsOrdinalIgnoreCaseForNonNumericCodes()
    {
        var cmp = SectionCodeHelper.SectionCodeComparer;
        Assert.Equal(0, cmp.Compare("pub-a", "PUB-A"));
    }

    [Fact]
    public void SectionCodeComparer_OrdersNullBeforeNonEmptyStrings()
    {
        var cmp = SectionCodeHelper.SectionCodeComparer;
        Assert.True(cmp.Compare(null, "1") < 0);
        Assert.True(cmp.Compare("1", null) > 0);
    }
}
