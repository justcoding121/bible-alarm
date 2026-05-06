#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheFileNamingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCacheFileName_throws_on_null_or_whitespace(string? lookUpPath)
    {
        Assert.Throws<ArgumentException>(() => MediaCacheFileNaming.GetCacheFileName(lookUpPath!));
    }

    [Fact]
    public void GetCacheFileName_is_stable_for_same_lookup_path()
    {
        const string path = "pub=nwt_E&track=1&langwritten=E";

        var a = MediaCacheFileNaming.GetCacheFileName(path);
        var b = MediaCacheFileNaming.GetCacheFileName(path);

        Assert.Equal(a, b);
        Assert.EndsWith(AppConstants.Media.MediaFileExtension, a, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MP4", AppConstants.Media.MediaVideoFileExtension)]
    [InlineData("mp4", AppConstants.Media.MediaVideoFileExtension)]
    [InlineData("MP3", AppConstants.Media.MediaFileExtension)]
    [InlineData("M4A", AppConstants.Media.MediaM4aFileExtension)]
    [InlineData("AAC", AppConstants.Media.MediaAacFileExtension)]
    public void GetCacheFileName_resolves_extension_from_query_fileformat(string formatValue, string expectedExt)
    {
        var path =
            $"?{AppConstants.Media.GetPubQueryParamName.FileFormat}={formatValue}&pub=x";

        var name = MediaCacheFileNaming.GetCacheFileName(path);

        Assert.EndsWith(expectedExt, name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://b.jw-cdn.org/a/file.mp4", AppConstants.Media.MediaVideoFileExtension)]
    [InlineData("https://b.jw-cdn.org/a/file.mp3", AppConstants.Media.MediaFileExtension)]
    [InlineData("http://app.jw-cdn.org/a/track.m4a", AppConstants.Media.MediaM4aFileExtension)]
    [InlineData("HTTPS://HOST/path.AAC", AppConstants.Media.MediaAacFileExtension)]
    public void GetCacheFileName_resolves_extension_from_absolute_uri_path(string url, string expectedExt)
    {
        var name = MediaCacheFileNaming.GetCacheFileName(url);

        Assert.EndsWith(expectedExt, name, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCacheFileName_defaults_to_mp3_when_format_not_in_lookup_path()
    {
        var name = MediaCacheFileNaming.GetCacheFileName("relative/lookup?output=json");

        Assert.EndsWith(AppConstants.Media.MediaFileExtension, name, StringComparison.Ordinal);
    }
}
