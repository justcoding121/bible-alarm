using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediaTrackTitleHelperTests
{
    [Fact]
    public void DecodeHtmlTitle_ReturnsUnknown_WhenNull()
    {
        Assert.Equal(MediaTrackTitleHelper.UnknownTitle, MediaTrackTitleHelper.DecodeHtmlTitle(null));
    }

    [Fact]
    public void DecodeHtmlTitleNullable_ReturnsNull_WhenRawNull()
    {
        Assert.Null(MediaTrackTitleHelper.DecodeHtmlTitleNullable(null));
    }

    [Fact]
    public void DecodeHtmlTitle_DecodesEntitiesAndSpaces()
    {
        Assert.Equal("A & B", MediaTrackTitleHelper.DecodeHtmlTitle("A &amp; B"));
        Assert.Equal("Hi there", MediaTrackTitleHelper.DecodeHtmlTitle("Hi\u00A0there"));
    }

    [Fact]
    public void DecodeHtmlTitleNullable_DoesNotFallBackToUnknown()
    {
        Assert.Equal("", MediaTrackTitleHelper.DecodeHtmlTitleNullable(""));
    }
}
