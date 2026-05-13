#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class LookupPathMediaFileExtensionResolverTests
{
    [Fact]
    public void Resolve_https_absolute_mp4_path_agrees_with_fileformat_mp4_fragment()
    {
        var fromPath = LookupPathMediaFileExtensionResolver.Resolve("https://cdn.example.org/media/track.MP4");
        var fromQuery = LookupPathMediaFileExtensionResolver.Resolve("?pub=x&fileformat=MP4");

        Assert.Equal(fromPath, fromQuery);
    }

    [Fact]
    public void Resolve_https_absolute_m4a_path_agrees_with_fileformat_m4a_fragment()
    {
        var fromPath = LookupPathMediaFileExtensionResolver.Resolve("https://files.example/a.M4a");
        var fromQuery = LookupPathMediaFileExtensionResolver.Resolve("prefixed fileformat=M4A suffix");

        Assert.Equal(fromPath, fromQuery);
    }

    [Fact]
    public void Resolve_plain_fragment_without_hints_matches_https_location_without_media_suffix()
    {
        var plain = LookupPathMediaFileExtensionResolver.Resolve("relative-path-without-uri");
        var httpsPlainPath = LookupPathMediaFileExtensionResolver.Resolve("https://example.org/virtual/folder/name");

        Assert.Equal(plain, httpsPlainPath);
    }

    [Fact]
    public void Resolve_throws_when_lookup_path_null()
    {
        Assert.Throws<ArgumentNullException>(() => LookupPathMediaFileExtensionResolver.Resolve(null!));
    }
}
