#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class ToastArtworkImageSrcResolverTests
{
    [Fact]
    public void TryResolve_returns_https_uri_unchanged()
    {
        const string url = "https://cdn.example.com/track.png";

        Assert.Equal(url, ToastArtworkImageSrcResolver.TryResolve(url));
    }

    [Fact]
    public void TryResolve_returns_null_when_artwork_blank()
    {
        Assert.Null(ToastArtworkImageSrcResolver.TryResolve("   "));
    }

    [Fact]
    public void TryResolve_maps_plain_relative_path_to_file_uri_when_possible()
    {
        Assert.True(LocalPathFileUriConverter.TryCreateUriString(Path.Combine("x", "cover.jpg"), out var expected));

        Assert.Equal(expected, ToastArtworkImageSrcResolver.TryResolve(Path.Combine("x", "cover.jpg")));
    }
}
