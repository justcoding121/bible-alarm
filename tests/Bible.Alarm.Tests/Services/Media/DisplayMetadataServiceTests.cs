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
}
