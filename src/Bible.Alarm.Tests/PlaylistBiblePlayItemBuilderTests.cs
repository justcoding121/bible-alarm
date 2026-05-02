#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class PlaylistBiblePlayItemBuilderTests
{
    private sealed class StubUrlConstruction : IUrlConstructionService
    {
        public string? PathToReturn { get; init; } = "?lookup=1";

        public Task<List<string>> ConstructTrackUrlsAsync(int trackId) =>
            Task.FromResult<List<string>>([]);

        public Task<List<string>> ConstructTrackUrlsAsync(
            string publicationCode,
            string languageCode,
            string? sectionCode,
            string trackCode) =>
            Task.FromResult<List<string>>([]);

        public Task<string?> ConstructTrackLookUpPathAsync(
            string publicationCode,
            string? languageCode,
            string? sectionCode,
            string trackCode) =>
            Task.FromResult(PathToReturn);

        public void ClearLookUpPathCache()
        {
        }
    }

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public string? UrlToReturn { get; init; } = "https://example.test/audio.mp3";

        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) =>
            Task.FromResult(UrlToReturn);
    }

    private sealed class CapturingUrlRefresh : IMediaUrlRefreshService
    {
        public TrackMetadata? LastMetadata { get; private set; }

        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata)
        {
            LastMetadata = trackMetadata;
            return Task.FromResult<string?>("https://example.test/iam.mp3");
        }
    }

    [Fact]
    public void Constructor_throws_when_dependency_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistBiblePlayItemBuilder(null!, new StubUrlRefresh()));

        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistBiblePlayItemBuilder(new StubUrlConstruction(), null!));
    }

    [Fact]
    public async Task BuildPlayItemAsync_returns_play_item_when_urls_resolve()
    {
        var builder = new PlaylistBiblePlayItemBuilder(new StubUrlConstruction(), new StubUrlRefresh());

        var playItem = await builder.BuildPlayItemAsync(
            scheduleId: 12,
            languageCode: "E",
            publicationCode: "nwtsty",
            sectionCode: "1",
            trackCode: "1");

        Assert.True(playItem.Metadata.IsBibleContent);
        Assert.Equal(12, playItem.Metadata.ScheduleId);
        Assert.Equal("https://example.test/audio.mp3", playItem.Url);
    }

    [Fact]
    public async Task BuildPlayItemAsync_throws_when_lookup_path_missing()
    {
        var builder = new PlaylistBiblePlayItemBuilder(
            new StubUrlConstruction { PathToReturn = "" },
            new StubUrlRefresh());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildPlayItemAsync(1, "E", "nwtsty", "1", "1"));
    }

    [Fact]
    public async Task BuildPlayItemAsync_throws_when_refresh_returns_empty()
    {
        var builder = new PlaylistBiblePlayItemBuilder(
            new StubUrlConstruction(),
            new StubUrlRefresh { UrlToReturn = "" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildPlayItemAsync(1, "E", "nwtsty", "1", "1"));
    }

    [Fact]
    public async Task BuildPlayItemAsync_applies_disc_style_download_code_before_url_refresh()
    {
        var capture = new CapturingUrlRefresh();
        var builder = new PlaylistBiblePlayItemBuilder(new StubUrlConstruction(), capture);

        await builder.BuildPlayItemAsync(44, "E", "iam", "iam-2", "07");

        Assert.NotNull(capture.LastMetadata);
        Assert.Equal("iam-2", capture.LastMetadata.DownloadCode);
        Assert.Equal(7, capture.LastMetadata.OriginalTrackCode);
    }
}
