#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MediaUrlRefreshServiceTests
{
    private static TrackMetadata Meta(string lookUpPath)
    {
        var meta = new TrackMetadata();
        meta.LookUpPath = lookUpPath;
        return meta;
    }

    [Fact]
    public async Task RefreshUrlAsync_returns_http_urls_as_is()
    {
        var sut = new MediaUrlRefreshService(TestLogging.CreateLogger());
        const string url = "https://cdn.example/track.mp3";

        Assert.Equal(url, await sut.RefreshUrlAsync(Meta(url)));
    }

    [Fact]
    public async Task RefreshUrlAsync_accepts_http_scheme_case_insensitively()
    {
        var sut = new MediaUrlRefreshService(TestLogging.CreateLogger());
        const string url = "HTTP://cdn.example/track.mp3";

        Assert.Equal(url, await sut.RefreshUrlAsync(Meta(url)));
    }

    [Fact]
    public async Task RefreshUrlAsync_returns_null_for_non_http_paths()
    {
        var sut = new MediaUrlRefreshService(TestLogging.CreateLogger());

        Assert.Null(await sut.RefreshUrlAsync(Meta("relative/path")));
    }

    [Fact]
    public async Task RefreshUrlAsync_returns_null_when_lookup_is_whitespace_only()
    {
        var sut = new MediaUrlRefreshService(TestLogging.CreateLogger());

        Assert.Null(await sut.RefreshUrlAsync(Meta("   ")));
    }
}
