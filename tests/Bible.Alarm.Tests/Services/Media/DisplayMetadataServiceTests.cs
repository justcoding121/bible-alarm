#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class DisplayMetadataServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_returns_metadata_for_idle_bible_catalog()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nw",
            TrackCode = "1",
            LookUpPath = "/test/path",
        };
        var item = new PlayItem(metadata, "file:///stub/bible-track.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_returns_metadata_for_idle_music_catalog()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = "songbook",
            TrackCode = "1",
            LookUpPath = "/music/path",
        };
        var item = new PlayItem(metadata, "file:///stub/music-track.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetDisplayMetadataAsync_populates_title_via_file_fallback_when_music_catalog_idle()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = string.Empty,
            PublicationCode = "iam",
            TrackCode = "1",
            LookUpPath = "/melody/path",
        };
        var item = new PlayItem(metadata, "file:///nonexistent-melody-track.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetDisplayMetadataAsync(track);

        Assert.Equal("Unknown Title", result.Title);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_https_streaming_uses_unknown_title_when_music_title_missing()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = string.Empty,
            PublicationCode = "iam",
            TrackCode = "1",
            LookUpPath = "/melody/path",
        };
        var item = new PlayItem(metadata, "https://example.invalid/stream.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.Equal("Unknown Title", result.Title);
        Assert.Equal("jw.org", result.Artist);
    }
}
