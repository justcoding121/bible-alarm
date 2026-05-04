#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

/// <summary>
/// <see cref="ArtworkManager"/> sets MAUI <see cref="ImageSource"/> instances; in the headless Windows test host,
/// <see cref="ImageSource.FromUri"/> / <see cref="ImageSource.FromFile"/> often yield null. These tests cover the
/// synchronous loading-flag and early-return branches without requiring a non-null image.
/// </summary>
public sealed class ArtworkManagerTests
{
    [Fact]
    public void UpdateArtwork_null_when_no_artwork_sets_loading_false()
    {
        var loadings = new List<bool>();
        var sut = new ArtworkManager(TestLogging.CreateLogger());

        sut.UpdateArtwork(null, _ => { }, loadings.Add);

        Assert.False(loadings[^1]);
        Assert.False(sut.HasArtwork);
    }

    [Fact]
    public void UpdateArtwork_unresolvable_url_without_fallback_clears_and_stops_loading()
    {
        ImageSource? bound = null;
        var loadings = new List<bool>();
        var sut = new ArtworkManager(TestLogging.CreateLogger());

        sut.UpdateArtwork(
            "totally-unusable-artwork-token",
            s => bound = s,
            loadings.Add);

        Assert.Null(bound);
        Assert.False(loadings[^1]);
        Assert.False(sut.HasArtwork);
    }

    [Fact]
    public void UpdateArtwork_force_reload_same_failed_url_does_not_throw()
    {
        var sut = new ArtworkManager(TestLogging.CreateLogger());

        sut.UpdateArtwork("https://example.com/z.png", _ => { }, _ => { });
        sut.UpdateArtwork("https://example.com/z.png", _ => { }, _ => { }, forceReload: true);

        Assert.False(sut.HasArtwork);
    }

    [Fact]
    public void UpdateArtwork_zero_byte_file_skips_load_and_clears()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alarm_artwork_empty_{Guid.NewGuid():N}.bin");
        File.WriteAllText(path, string.Empty);
        try
        {
            var loadings = new List<bool>();
            var sut = new ArtworkManager(TestLogging.CreateLogger());

            sut.UpdateArtwork(path, _ => { }, loadings.Add);

            Assert.False(sut.HasArtwork);
            Assert.False(loadings[^1]);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // best effort
            }
        }
    }

    [Fact]
    public void UpdateArtwork_nonexistent_rooted_path_clears()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing_artwork_{Guid.NewGuid():N}.png");
        var loadings = new List<bool>();
        var sut = new ArtworkManager(TestLogging.CreateLogger());

        sut.UpdateArtwork(missing, _ => { }, loadings.Add);

        Assert.False(sut.HasArtwork);
        Assert.False(loadings[^1]);
    }

    [Fact]
    public void UpdateArtwork_uses_file_scheme_uri_when_file_exists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alarm_artwork_bin_{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, [0x01, 0x02]);
        try
        {
            var fileUrl = new Uri(path).AbsoluteUri;
            Assert.StartsWith(MediaUriSchemeConstants.FilePrefix, fileUrl, StringComparison.OrdinalIgnoreCase);

            ImageSource? bound = null;
            var loadings = new List<bool>();
            var sut = new ArtworkManager(TestLogging.CreateLogger());

            sut.UpdateArtwork(fileUrl, s => bound = s, loadings.Add);

            _ = bound;
            Assert.False(loadings[^1]);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void UpdateArtwork_primary_failure_loads_https_fallback()
    {
        var loadings = new List<bool>();
        var sut = new ArtworkManager(TestLogging.CreateLogger());

        sut.UpdateArtwork(
            "not-a-uri-or-path",
            _ => { },
            loadings.Add,
            fallbackUrl: "https://example.com/fallback.png");

        Assert.False(loadings[^1]);
    }

    [Fact]
    public void UpdateArtwork_empty_string_clears_like_null_when_no_previous_artwork()
    {
        var loadings = new List<bool>();
        var sut = new ArtworkManager(TestLogging.CreateLogger());

        sut.UpdateArtwork(string.Empty, _ => { }, loadings.Add);

        Assert.False(loadings[^1]);
        Assert.False(sut.HasArtwork);
    }

    [Fact]
    public void UpdateArtwork_when_fallback_url_equals_primary_url_clears()
    {
        const string token = "same-token-neither-loads-as-uri-or-file";
        var loadings = new List<bool>();
        var sut = new ArtworkManager(TestLogging.CreateLogger());

        sut.UpdateArtwork(token, _ => { }, loadings.Add, fallbackUrl: token);

        Assert.False(sut.HasArtwork);
        Assert.False(loadings[^1]);
    }

    [Fact]
    public void UpdateArtwork_setting_null_after_uri_attempt_sends_null_to_binder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alarm_artwork_then_null_{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, [0x01]);
        try
        {
            var fileUrl = new Uri(path).AbsoluteUri;
            var sources = new List<ImageSource?>();
            var sut = new ArtworkManager(TestLogging.CreateLogger());

            sut.UpdateArtwork(fileUrl, sources.Add, _ => { });
            sut.UpdateArtwork(null, sources.Add, _ => { });

            Assert.NotEmpty(sources);
            Assert.Null(sources[^1]);
            Assert.False(sut.HasArtwork);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
