#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediaLookupPathCacheKeyComposerTests
{
    [Fact]
    public void ComposeCacheFileName_suffixes_token_with_resolver_extension_for_https_track()
    {
        var lookupPath = "https://cdn.example.org/audio/track.mp3";
        var extension = LookupPathMediaFileExtensionResolver.Resolve(lookupPath);

        var fileName = MediaLookupPathCacheKeyComposer.ComposeCacheFileName(lookupPath);

        Assert.EndsWith(extension, fileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(fileName.Length > extension.Length);
    }

    [Fact]
    public void ComposeCacheFileName_throws_when_lookup_path_whitespace_only()
    {
        Assert.Throws<ArgumentException>(() => MediaLookupPathCacheKeyComposer.ComposeCacheFileName("   "));
    }
}
