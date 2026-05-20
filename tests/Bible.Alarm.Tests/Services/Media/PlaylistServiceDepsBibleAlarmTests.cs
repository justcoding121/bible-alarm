#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaylistServiceDepsBibleAlarmTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }

    private sealed class StubUrlRefresh : IMediaUrlRefreshService
    {
        public Task<string?> RefreshUrlAsync(Shared.Models.Media.TrackMetadata trackMetadata) =>
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
#pragma warning restore CS0067

    [Fact]
    public void Record_stores_all_dependency_references()
    {
        var logger = TestLogging.CreateLogger();
        var media = new IdleCatalogMediaService();
        var dispatcher = new NopDispatcher();
        var urlRefresh = new StubUrlRefresh();
        var urlBuild = new StubUrlConstruction();

        var deps = new PlaylistServiceDeps(
            logger,
            media,
            dispatcher,
            null!,
            null!,
            null!,
            null!,
            urlRefresh,
            urlBuild);

        Assert.Same(logger, deps.Logger);
        Assert.Same(media, deps.MediaService);
        Assert.Same(dispatcher, deps.Dispatcher);
        Assert.Same(urlRefresh, deps.UrlRefreshService);
        Assert.Same(urlBuild, deps.UrlConstructionService);
        Assert.Null(deps.LanguageContentService);
        Assert.Null(deps.ScopeFactory);
        Assert.Null(deps.ScheduleDisplayNameService);
    }
}
