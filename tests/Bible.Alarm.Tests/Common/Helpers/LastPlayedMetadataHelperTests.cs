#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class LastPlayedMetadataHelperTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", null)]
    [InlineData(null, "   ")]
    public void SaveLastPlayedMetadata_no_op_when_no_title_or_artist(string? title, string? artist)
    {
        var ex = Record.Exception(() => LastPlayedMetadataHelper.SaveLastPlayedMetadata(title, artist));
        Assert.Null(ex);
    }

    [Fact]
    public void SaveLastPlayedMetadata_catches_when_di_preferences_unavailable()
    {
        var ex = Record.Exception(() =>
            LastPlayedMetadataHelper.SaveLastPlayedMetadata("A title", null));

        Assert.Null(ex);
    }
}
