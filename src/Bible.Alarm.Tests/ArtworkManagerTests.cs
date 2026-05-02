#nullable enable

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
}
