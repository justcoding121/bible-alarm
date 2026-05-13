#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaylistServiceTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public Task<string?> RefreshUrlAsync(TrackMetadata trackMetadata) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubUrlConstruction : IUrlConstructionService
    {
        public Task<List<string>> ConstructTrackUrlsAsync(int trackId) =>
            Task.FromResult<List<string>>([]);

        public Task<List<string>> ConstructTrackUrlsAsync(string publicationCode, string languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<List<string>>([]);

        public Task<string?> ConstructTrackLookUpPathAsync(string publicationCode, string? languageCode, string? sectionCode, string trackCode) =>
            Task.FromResult<string?>(null);

        public void ClearLookUpPathCache()
        {
        }
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var deps = new PlaylistServiceDeps(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new NopDispatcher(),
            null!,
            null!,
            null!,
            null!,
            new StubUrlRefresh(),
            new StubUrlConstruction(),
            LanguageContentService: null,
            ScopeFactory: null,
            ScheduleDisplayNameService: null);

        var sut = new PlaylistService(deps);

        Assert.NotNull(sut);
    }
}
