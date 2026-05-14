#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Fluxor;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicCascadeHandlerTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class IdleMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private sealed class IdleLanguageContentService : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchPublicationSectionsAsync(string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchSectionTracksAsync(string publicationCode, string sectionCode, string languageCode,
            bool replaceExistingTracksFromApi = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsurePublicationExistsAsync(string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(string languageCode, string? categoryName = null, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class UnexpectedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Scope must not be created when cascade exits early.");
    }

    private sealed class FailingMediaScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("DB unavailable");
    }

    private static (DbContextOptions<MediaDbContext> Options, SqliteConnection KeepAlive) CreateInMemoryOptions()
    {
        var dbName = $"TestDb_{Guid.NewGuid():N}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        // Keep one connection open so the named in-memory database is not destroyed between EnsureCreated and the test query.
        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connectionString)
            .Options;
        using var ctx = new MediaDbContext(options);
        ctx.Database.EnsureCreated();
        return (options, keepAlive);
    }

    [Fact]
    public async Task HandleAsync_no_op_when_current_schedule_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var logger = TestLogging.CreateLogger();
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([])),
            new UnexpectedScopeFactory(),
            logger);

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_swallows_exception_when_language_cascade_cannot_open_scope()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            MusicEnabled = true,
            MusicPublicationCode = "",
            MusicLanguageCode = "E",
        };
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([], current)),
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_noops_when_everything_already_set_for_flat_publication()
    {
        var (options, connection) = CreateInMemoryOptions();
        using var _ = connection;
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            MusicEnabled = true,
            MusicPublicationCode = "osg",
            MusicTrackCode = "1",
            MusicPublicationModalItemCount = 0,
            MusicSectionModalItemCount = 0,
        };
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([], current)),
            new MediaTestScopeFactory(options),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_dispatches_UpdateScheduleAction_when_modal_counts_stale_for_flat_pub()
    {
        var (options, connection) = CreateInMemoryOptions();
        using var _ = connection;
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            MusicEnabled = true,
            MusicPublicationCode = "osg",
            MusicTrackCode = "1",
            MusicPublicationModalItemCount = 99,
            MusicSectionModalItemCount = 0,
        };
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([], current)),
            new MediaTestScopeFactory(options),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        var action = Assert.Single(dispatcher.Dispatched);
        Assert.IsType<UpdateScheduleFromViewModelAction>(action);
    }

    [Fact]
    public async Task HandleAsync_noops_when_sectioned_pub_section_and_track_set()
    {
        var (options, connection) = CreateInMemoryOptions();
        using var _ = connection;
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            MusicEnabled = true,
            MusicPublicationCode = "iam",
            MusicSectionCode = "iam-9",
            MusicTrackCode = "1",
            MusicPublicationModalItemCount = 0,
            MusicSectionModalItemCount = 0,
        };
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([], current)),
            new MediaTestScopeFactory(options),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_exits_early_when_sectioned_pub_no_section_code()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            MusicEnabled = true,
            MusicPublicationCode = "iam",
            MusicSectionCode = null,
            MusicTrackCode = null,
        };
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([], current)),
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_exits_early_when_sectioned_pub_has_section_but_no_track()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            MusicEnabled = true,
            MusicPublicationCode = "iam",
            MusicSectionCode = "iam-1",
            MusicTrackCode = null,
        };
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([], current)),
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_swallows_unhandled_exception_from_modal_count_refresh()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem
        {
            MusicEnabled = true,
            MusicPublicationCode = "osg",
            MusicTrackCode = "1",
        };
        var handler = new MusicCascadeHandler(
            new IdleMediaService(),
            new IdleLanguageContentService(),
            new FakeApplicationState(new ApplicationState([], current)),
            new FailingMediaScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }
}
