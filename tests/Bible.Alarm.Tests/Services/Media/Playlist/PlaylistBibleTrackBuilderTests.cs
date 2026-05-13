#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class PlaylistBibleTrackBuilderTests
{
    private sealed class StubUrlConstruction : IUrlConstructionService
    {
        public Task<List<string>> ConstructTrackUrlsAsync(int trackId) =>
            Task.FromResult<List<string>>([]);

        public Task<List<string>> ConstructTrackUrlsAsync(string publicationCode, string languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<List<string>>([]);

        public Task<string?> ConstructTrackLookUpPathAsync(string publicationCode, string? languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<string?>("/lookup");

        public void ClearLookUpPathCache()
        {
        }
    }

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) =>
            Task.FromResult<string?>("https://example.test/track.mp3");
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new PlaylistBiblePublicationTrackBuilder(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new StubUrlRefresh(),
            new StubUrlConstruction());

        Assert.NotNull(sut);
    }
}
