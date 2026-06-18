#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class ToastArtworkImageSrcResolverBibleAlarmTests
{
    [Fact]
    public void TryResolve_returns_null_for_blank_artwork()
    {
        Assert.Null(ToastArtworkImageSrcResolver.TryResolve("  "));
    }

    [Fact]
    public void TryResolve_returns_null_for_null_artwork()
    {
        Assert.Null(ToastArtworkImageSrcResolver.TryResolve(null));
    }

    [Fact]
    public void TryResolve_returns_https_uri_unchanged()
    {
        const string url = "https://example.com/art.png";
        Assert.Equal(url, ToastArtworkImageSrcResolver.TryResolve(url));
    }

    [Fact]
    public void TryResolve_maps_existing_local_path_to_file_uri()
    {
        var path = Path.Combine(Path.GetTempPath(), "toast-artwork-test.txt");
        File.WriteAllText(path, "x");
        try
        {
            var resolved = ToastArtworkImageSrcResolver.TryResolve(path);
            Assert.NotNull(resolved);
            Assert.StartsWith("file://", resolved, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryResolve_returns_null_when_local_path_cannot_be_mapped()
    {
        Assert.Null(ToastArtworkImageSrcResolver.TryResolve("bad-path\0name"));
    }
}
