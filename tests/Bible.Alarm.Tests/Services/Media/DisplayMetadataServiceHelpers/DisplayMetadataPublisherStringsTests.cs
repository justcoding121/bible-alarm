#nullable enable

using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;

namespace Bible.Alarm.Tests;

public sealed class DisplayMetadataPublisherStringsTests
{
    [Fact]
    public void String_constants_have_expected_values()
    {
        Assert.Equal("jw.org", DisplayMetadataPublisherStrings.JwOrgLabel);
        Assert.Equal(" (jw.org)", DisplayMetadataPublisherStrings.JwOrgArtistQualifier);
        Assert.False(string.IsNullOrWhiteSpace(DisplayMetadataPublisherStrings.JwOrgLabel));
        Assert.False(string.IsNullOrWhiteSpace(DisplayMetadataPublisherStrings.JwOrgArtistQualifier));
    }
}
