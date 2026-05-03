#nullable enable

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediaPlaybackConstantsTests
{
    [Fact]
    public void MediaUriSchemeConstants_PublishStablePrefixes()
    {
        Assert.Equal("file://", MediaUriSchemeConstants.FilePrefix);
        Assert.Equal("https://", MediaUriSchemeConstants.HttpsPrefix);
        Assert.Equal("http://", MediaUriSchemeConstants.HttpPrefix);
    }

    [Fact]
    public void MediaUriSchemeConstants_FileUriTripleSlash_PrefixesTripleSlashAuthorityStyle()
        => Assert.Equal(
            $"{MediaUriSchemeConstants.FilePrefix}{Path.AltDirectorySeparatorChar}",
            MediaUriSchemeConstants.FileUriTripleSlashPrefix);

    [Fact]
    public void TagLibMimeConstants_AudioAndVideoIdentifiers()
    {
        Assert.Equal("audio/mpeg", TagLibMimeConstants.AudioMpeg);
        Assert.Equal("video/mp4", TagLibMimeConstants.VideoMp4);
    }
}
