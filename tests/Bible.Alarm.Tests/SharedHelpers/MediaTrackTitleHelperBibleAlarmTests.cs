#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class MediaTrackTitleHelperBibleAlarmTests
{
    [Fact]
    public void DecodeHtmlTitleNullable_returns_decoded_value_when_title_present()
    {
        Assert.Equal("A & B", MediaTrackTitleHelper.DecodeHtmlTitleNullable("A &amp; B"));
    }
}
