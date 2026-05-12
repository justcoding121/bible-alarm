#nullable enable

using System.IO;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediaPlaybackConstantsTests
{
    [Fact]
    public void FileUriTripleSlashPrefix_composes_from_file_prefix_and_alt_separator()
    {
        Assert.Equal(
            MediaUriSchemeConstants.FilePrefix + Path.AltDirectorySeparatorChar,
            MediaUriSchemeConstants.FileUriTripleSlashPrefix);
    }

    [Fact]
    public void Https_and_http_prefixes_share_colon_double_slash_suffix()
    {
        Assert.EndsWith("://", MediaUriSchemeConstants.HttpsPrefix, StringComparison.Ordinal);
        Assert.EndsWith("://", MediaUriSchemeConstants.HttpPrefix, StringComparison.Ordinal);
    }

    [Fact]
    public void TagLibMime_constants_use_primary_type_subtype_segments()
    {
        Assert.Equal(2, TagLibMimeConstants.AudioMpeg.Split('/').Length);
        Assert.Equal(2, TagLibMimeConstants.VideoMp4.Split('/').Length);
        Assert.NotEqual(TagLibMimeConstants.AudioMpeg, TagLibMimeConstants.VideoMp4);
    }
}
