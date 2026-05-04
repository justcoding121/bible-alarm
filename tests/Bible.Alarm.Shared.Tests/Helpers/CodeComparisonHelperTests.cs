using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class CodeComparisonHelperTests
{
    [Fact]
    public void Equals_True_WhenBothNull()
    {
        Assert.True(CodeComparisonHelper.Equals(null, null));
    }

    [Fact]
    public void Compare_ReturnsZero_WhenBothNull()
    {
        Assert.Equal(0, CodeComparisonHelper.Compare(null, null));
    }

    [Theory]
    [InlineData(null, "a")]
    [InlineData("a", null)]
    public void Equals_False_WhenExactlyOneSideNull(string? a, string? b)
    {
        Assert.False(CodeComparisonHelper.Equals(a, b));
    }

    [Theory]
    [InlineData(null, "b", -1)]
    [InlineData("a", null, 1)]
    public void Compare_OrdersNullBeforeNonNull(string? a, string? b, int sign)
    {
        var c = CodeComparisonHelper.Compare(a, b);
        Assert.True(sign < 0 ? c < 0 : c > 0);
    }

    [Theory]
    [InlineData("1", "01", true)]
    [InlineData("2", "10", false)]
    [InlineData("abc", "ABC", true)]
    public void Equals_UsesNumericVersusOrdinalIgnoreCaseLogic(string a, string b, bool equal)
    {
        Assert.Equal(equal, CodeComparisonHelper.Equals(a, b));
    }

    [Fact]
    public void Compare_ComparesInts_WhenBothParseAsNumbers()
    {
        Assert.True(CodeComparisonHelper.Compare("3", "10") < 0);
        Assert.True(CodeComparisonHelper.Compare("010", "8") > 0);
    }

    [Fact]
    public void Compare_StringFallback_IsOrdinalIgnoreCase()
    {
        Assert.Equal(0, CodeComparisonHelper.Compare("Jw", "jW"));
    }

    [Theory]
    [InlineData("Jw", "10")]
    [InlineData("10", "Jw")]
    public void Compare_UsesOrdinalIgnoreCase_WhenOneOrBothSidesAreNotInts(string left, string right)
    {
        var c = CodeComparisonHelper.Compare(left, right);
        var mirrored = CodeComparisonHelper.Compare(right, left);
        Assert.True(c != 0);
        Assert.Equal(-Math.Sign(c), Math.Sign(mirrored));
        Assert.True(CodeComparisonHelper.Equals(left, left));
        Assert.True(CodeComparisonHelper.Equals(right, right));
        Assert.False(CodeComparisonHelper.Equals(left, right));
    }

    [Fact]
    public void Compare_AndEquals_StringPath_WhenValueDoesNotFitInt32()
    {
        var tooWide = ((long)int.MaxValue + 1L).ToString();
        Assert.False(int.TryParse(tooWide, out _));
        Assert.Equal(0, CodeComparisonHelper.Compare(tooWide, tooWide));
        Assert.True(CodeComparisonHelper.Equals(tooWide, tooWide.ToUpperInvariant()));
    }

    [Fact]
    public void Compare_NegativeVersusPositive_UsesInvariantIntegerSemantics()
    {
        Assert.True(CodeComparisonHelper.Compare("-1", "1") < 0);
        Assert.False(CodeComparisonHelper.Equals("-3", "+3"));
    }

    [Fact]
    public void Equals_WithLeadingWhitespace_SeesSameInvariantIntegerWhenParsingSucceeds()
    {
        Assert.True(CodeComparisonHelper.Equals(" 42", "+42"));
        Assert.Equal(0, CodeComparisonHelper.Compare("\t010", "+10")); // LeadingWhite + LeadingSign
    }

    [Fact]
    public void Compare_WithNonDigitSuffixUsesOrdinalIgnoreCasePath()
    {
        Assert.False(CodeComparisonHelper.Equals("01a", "01b")); // Neither side is a lone int32 token
        Assert.True(CodeComparisonHelper.Compare("episode-01", "Episode-09") < 0);
    }
}
