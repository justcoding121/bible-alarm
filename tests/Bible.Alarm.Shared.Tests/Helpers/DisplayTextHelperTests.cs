using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class DisplayTextHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeSingleLine_ReturnsEmpty_ForBlank(string? input)
    {
        Assert.Equal(string.Empty, DisplayTextHelper.NormalizeSingleLine(input));
    }

    [Fact]
    public void NormalizeSingleLine_TrimsAndCollapsesWhitespace()
    {
        Assert.Equal("One Two", DisplayTextHelper.NormalizeSingleLine("  One\r\nTwo  "));
        Assert.Equal("A B", DisplayTextHelper.NormalizeSingleLine("A\t \tB"));
    }

    [Fact]
    public void NormalizeSingleLine_ReplacesNonBreakingSpace()
    {
        Assert.Equal("x y", DisplayTextHelper.NormalizeSingleLine("x\u00A0\u00A0y"));
    }

    [Fact]
    public void NormalizeSingleLine_StripsControlCharactersOtherThanTypicalWhitespace()
    {
        Assert.Equal("Hi", DisplayTextHelper.NormalizeSingleLine("Hi\u0001"));
    }

    [Fact]
    public void NormalizeSingleLine_CollapsesVerticalTabBetweenWordsToSingleSpace()
    {
        Assert.Equal("A B", DisplayTextHelper.NormalizeSingleLine($"A{'\u000B'}B"));
    }

    [Fact]
    public void NormalizeSingleLine_ReturnsEmpty_WhenOnlyNonBreakingSpaces()
    {
        Assert.Equal(string.Empty, DisplayTextHelper.NormalizeSingleLine("\u00A0\u00A0\u00A0"));
    }
}
