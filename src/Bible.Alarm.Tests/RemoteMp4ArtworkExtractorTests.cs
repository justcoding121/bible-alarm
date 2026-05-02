#nullable enable

using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class RemoteMp4ArtworkExtractorTests
{
    [Fact]
    public async Task TryExtractMetadata_returns_null_for_non_mp4_url()
    {
        using var handler = new HttpClientHandler();
        var sut = new RemoteMp4ArtworkExtractor(handler, TestLogging.CreateLogger());

        Assert.Null(await sut.TryExtractMetadataAsync("https://cdn.example/track.mp3"));
    }

    [Fact]
    public async Task TryExtractMetadata_returns_null_for_short_and_empty_urls()
    {
        using var handler = new HttpClientHandler();
        var sut = new RemoteMp4ArtworkExtractor(handler, TestLogging.CreateLogger());

        Assert.Null(await sut.TryExtractMetadataAsync(""));
        Assert.Null(await sut.TryExtractMetadataAsync("x.mp"));
    }
}
