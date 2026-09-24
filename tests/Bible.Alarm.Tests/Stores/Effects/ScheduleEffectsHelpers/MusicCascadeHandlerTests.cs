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
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Tests.Support;
using Fluxor;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;
using CascadeFixtures = Bible.Alarm.Tests.Support.CascadeHandlerTestFixtures;

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

    private sealed class InMemoryDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MediaDbContext _schemaHolder;

        public DbContextOptions<MediaDbContext> Options { get; }

        public InMemoryDb()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            Options = new DbContextOptionsBuilder<MediaDbContext>()
                .UseSqlite(_connection)
                .Options;
            _schemaHolder = new MediaDbContext(Options);
            _schemaHolder.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _schemaHolder.Dispose();
            _connection.Dispose();
        }
    }

    private static InMemoryDb CreateInMemoryOptions() => new InMemoryDb();

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
        using var db = CreateInMemoryOptions();
        var options = db.Options;
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
        using var db = CreateInMemoryOptions();
        var options = db.Options;
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
        using var db = CreateInMemoryOptions();
        var options = db.Options;
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

    [Fact]
    public async Task HandleAsync_language_cascade_dispatches_first_flat_publication_and_track()
    {
        const string pub = "sjjc";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedMusicLanguageBoundPublicationAsync(options, pub, "E", "Sing", includePublicationRow: true);
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem
            {
                MusicEnabled = true,
                MusicLanguageCode = "E",
                MusicPublicationCode = null,
            };
            var media = new CascadeFixtures.FlatTracksMediaService("E", pub);
            var handler = new MusicCascadeHandler(
                media,
                new CascadeFixtures.ConfigurableLanguageContentService(ensureExists: true),
                new FakeApplicationState(new ApplicationState([], current)),
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger());

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal(pub, update.Schedule.MusicPublicationCode);
            Assert.Equal("2", update.Schedule.MusicTrackCode);
            Assert.True(update.MusicUpdated);
        }
    }

    [Fact]
    public async Task HandleAsync_language_cascade_no_language_publication_dispatches_via_custom_path()
    {
        const string pub = "iam";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedMusicFlatPublicationAsync(options, pub, "Kingdom Melodies");
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem
            {
                MusicEnabled = true,
                MusicLanguageCode = "F",
                MusicLanguageName = "French",
                MusicPublicationCode = null,
            };
            var sectionCode = "iam-1";
            var media = new CascadeFixtures.SectionTracksMediaService("E", pub, sectionCode, withoutLanguage: true);
            var handler = new MusicCascadeHandler(
                media,
                new IdleLanguageContentService(),
                new FakeApplicationState(new ApplicationState([], current)),
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger());

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal(pub, update.Schedule.MusicPublicationCode);
            Assert.Equal("F", update.Schedule.MusicLanguageCode);
            Assert.Equal("1", update.Schedule.MusicTrackCode);
        }
    }

    [Fact]
    public async Task HandleAsync_flat_publication_cascade_dispatches_when_track_missing()
    {
        const string pub = "osg";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedMusicFlatPublicationAsync(options, pub, "Organ");
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem
            {
                MusicEnabled = true,
                MusicPublicationCode = pub,
                MusicTrackCode = null,
            };
            var media = new CascadeFixtures.FlatTracksMediaService(string.Empty, pub, withoutLanguage: true);
            var handler = new MusicCascadeHandler(
                media,
                new IdleLanguageContentService(),
                new FakeApplicationState(new ApplicationState([], current)),
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger());

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal("2", update.Schedule.MusicTrackCode);
            Assert.Null(update.Schedule.MusicSectionCode);
        }
    }

    [Fact]
    public async Task HandleAsync_publication_cascade_dispatches_section_and_track_for_sectioned_publication()
    {
        const string pub = "iam";
        const string section = "iam-1";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedMusicSectionedPublicationAsync(options, pub, withoutLanguage: true);
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem
            {
                MusicEnabled = true,
                MusicPublicationCode = pub,
                MusicSectionCode = null,
                MusicTrackCode = null,
            };
            var media = new CascadeFixtures.SectionTracksMediaService(string.Empty, pub, section, withoutLanguage: true);
            var handler = new MusicCascadeHandler(
                media,
                new IdleLanguageContentService(),
                new FakeApplicationState(new ApplicationState([], current)),
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger());

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal(section, update.Schedule.MusicSectionCode);
            Assert.Equal("1", update.Schedule.MusicTrackCode);
        }
    }

    [Fact]
    public async Task HandleAsync_section_cascade_dispatches_first_track_when_section_set()
    {
        const string pub = "iam";
        const string section = "iam-2";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedMusicSectionedPublicationAsync(options, pub, withoutLanguage: false);
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem
            {
                MusicEnabled = true,
                MusicLanguageCode = "E",
                MusicPublicationCode = pub,
                MusicSectionCode = section,
                MusicTrackCode = null,
            };
            var media = new CascadeFixtures.SectionTracksMediaService("E", pub, section);
            var handler = new MusicCascadeHandler(
                media,
                new IdleLanguageContentService(),
                new FakeApplicationState(new ApplicationState([], current)),
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger());

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal("1", update.Schedule.MusicTrackCode);
        }
    }

    [Fact]
    public async Task HandleAsync_language_cascade_exits_when_catalog_fails()
    {
        const string pub = "vocal-fail";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedMusicLanguageBoundPublicationAsync(options, pub, "E", "Vocal", includePublicationRow: false);
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem { MusicEnabled = true, MusicLanguageCode = "E" };
            var handler = new MusicCascadeHandler(
                new IdleMediaService(),
                new CascadeFixtures.ConfigurableLanguageContentService(ensureExists: false),
                new FakeApplicationState(new ApplicationState([], current)),
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger());

            await handler.HandleAsync(dispatcher);

            Assert.Empty(dispatcher.Dispatched);
        }
    }
}
