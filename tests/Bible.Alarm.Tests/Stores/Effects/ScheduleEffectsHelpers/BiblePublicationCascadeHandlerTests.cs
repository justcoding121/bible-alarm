#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;
using CascadeFixtures = Bible.Alarm.Tests.Support.CascadeHandlerTestFixtures;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationCascadeHandlerTests
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
            throw new InvalidOperationException("Cascade handler must not open a scope when CurrentSchedule is null.");
    }

    private sealed class UnexpectedBiblePublicationService : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromException<BiblePublication?>(new InvalidOperationException("unexpected"));

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromException<BiblePublication?>(new InvalidOperationException("unexpected"));

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromException<Dictionary<string, BiblePublication>>(new InvalidOperationException("unexpected"));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromException<Dictionary<string, Language>>(new InvalidOperationException("unexpected"));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromException<List<string>>(new InvalidOperationException("unexpected"));

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromException<string?>(new InvalidOperationException("unexpected"));

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromException<bool>(new InvalidOperationException("unexpected"));

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromException<(string? CategoryCode, bool IsMusic)?>(new InvalidOperationException("unexpected"));

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromException<List<string>>(new InvalidOperationException("unexpected"));

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    [Fact]
    public async Task HandleAsync_no_op_when_current_schedule_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            new FakeApplicationState(new ApplicationState([])),
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var handler = new BiblePublicationCascadeHandler(
            new UnexpectedBiblePublicationService(),
            new IdleMediaService(),
            new IdleLanguageContentService(),
            itemSelector,
            new FakeApplicationState(new ApplicationState([])),
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_swallows_exception_when_language_set_but_publication_empty_and_scope_throws()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = null
        };
        var dispatcher = new RecordingDispatcher();
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: schedule));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            state,
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var handler = new BiblePublicationCascadeHandler(
            new UnexpectedBiblePublicationService(),
            new IdleMediaService(),
            new IdleLanguageContentService(),
            itemSelector,
            state,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_swallows_exception_when_publication_set_sectionCode_empty_scope_throws()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCode = "nwtsty",
            BiblePublicationSectionCode = null,
            BiblePublicationTrackCode = null
        };
        var dispatcher = new RecordingDispatcher();
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: schedule));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            state,
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var handler = new BiblePublicationCascadeHandler(
            new UnexpectedBiblePublicationService(),
            new IdleMediaService(),
            new IdleLanguageContentService(),
            itemSelector,
            state,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_swallows_exception_when_section_set_but_trackCode_empty_scope_throws()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCode = "nwtsty",
            BiblePublicationSectionCode = "1",
            BiblePublicationTrackCode = null
        };
        var dispatcher = new RecordingDispatcher();
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: schedule));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            state,
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var handler = new BiblePublicationCascadeHandler(
            new UnexpectedBiblePublicationService(),
            new IdleMediaService(),
            new IdleLanguageContentService(),
            itemSelector,
            state,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_noop_when_all_fields_populated()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "nwtsty",
            BiblePublicationSectionCode = "1",
            BiblePublicationTrackCode = "1"
        };
        var dispatcher = new RecordingDispatcher();
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: schedule));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            state,
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var handler = new BiblePublicationCascadeHandler(
            new UnexpectedBiblePublicationService(),
            new IdleMediaService(),
            new IdleLanguageContentService(),
            itemSelector,
            state,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_noop_when_languageCode_also_empty_no_fields_set()
    {
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = null,
            BiblePublicationCode = null,
            BiblePublicationSectionCode = null,
            BiblePublicationTrackCode = null
        };
        var dispatcher = new RecordingDispatcher();
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: schedule));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            state,
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var handler = new BiblePublicationCascadeHandler(
            new UnexpectedBiblePublicationService(),
            new IdleMediaService(),
            new IdleLanguageContentService(),
            itemSelector,
            state,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    private static BiblePublicationCascadeHandler CreateHandler(
        ScheduleStateItem schedule,
        IMediaService media,
        ILanguageContentService languageContent,
        IBiblePublicationService biblePublicationService,
        IServiceScopeFactory scopeFactory)
    {
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: schedule));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            media,
            state,
            biblePublicationService: biblePublicationService,
            biblePublicationSectionService: null,
            languageContentService: languageContent,
            scopeFactory: scopeFactory);
        return new BiblePublicationCascadeHandler(
            biblePublicationService,
            media,
            languageContent,
            itemSelector,
            state,
            scopeFactory,
            TestLogging.CreateLogger());
    }

    [Fact]
    public async Task HandleAsync_section_cascade_dispatches_first_track()
    {
        const string pub = "nwtsty";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            var dispatcher = new RecordingDispatcher();
            var schedule = new ScheduleStateItem
            {
                BiblePublicationLanguageCode = "E",
                BiblePublicationCode = pub,
                BiblePublicationSectionCode = "1",
                BiblePublicationTrackCode = null,
            };
            var media = new CascadeFixtures.SectionTracksMediaService("E", pub, "1");
            var handler = CreateHandler(
                schedule,
                media,
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                new CascadeFixtures.CountingBiblePublicationService([pub]),
                new MediaTestScopeFactory(options));

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal("1", update.Schedule.BiblePublicationTrackCode);
        }
    }

    [Fact]
    public async Task HandleAsync_publication_cascade_dispatches_flat_track()
    {
        const string pub = "sjjc";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedBibleFlatPublicationAsync(options, pub, "E");
            var dispatcher = new RecordingDispatcher();
            var schedule = new ScheduleStateItem
            {
                BiblePublicationLanguageCode = "E",
                BiblePublicationCode = pub,
                BiblePublicationSectionCode = null,
                BiblePublicationTrackCode = null,
            };
            var scopeFactory = new MediaTestScopeFactory(options);
            var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            };
            var media = new CascadeFixtures.FlatTracksMediaService("E", pub);
            var handler = CreateHandler(
                schedule,
                media,
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                new CascadeFixtures.DbBackedBiblePublicationService(scopeFactory, languages, [pub]),
                scopeFactory);

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal("1", update.Schedule.BiblePublicationTrackCode);
        }
    }

    [Fact]
    public async Task HandleAsync_language_cascade_resolves_publication_from_database()
    {
        const string pub = "sjjc";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedBibleFlatPublicationAsync(options, pub, "E");
            var dispatcher = new RecordingDispatcher();
            var schedule = new ScheduleStateItem
            {
                BiblePublicationLanguageCode = "E",
                BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
                BiblePublicationCode = null,
            };
            var scopeFactory = new MediaTestScopeFactory(options);
            var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            };
            var media = new CascadeFixtures.FlatTracksMediaService("E", pub);
            var handler = CreateHandler(
                schedule,
                media,
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                new CascadeFixtures.DbBackedBiblePublicationService(scopeFactory, languages, [pub]),
                scopeFactory);

            await handler.HandleAsync(dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal(pub, update.Schedule.BiblePublicationCode);
            Assert.Equal("1", update.Schedule.BiblePublicationTrackCode);
        }
    }

    [Fact]
    public async Task HandleAsync_language_cascade_noop_when_publication_languages_not_queryable()
    {
        const string pub = "sjjc";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedMusicLanguageBoundPublicationAsync(options, pub, "E", "Wrong cat", includePublicationRow: true);
            var dispatcher = new RecordingDispatcher();
            var schedule = new ScheduleStateItem
            {
                BiblePublicationLanguageCode = "E",
                BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
                BiblePublicationCode = null,
            };
            var handler = CreateHandler(
                schedule,
                new IdleMediaService(),
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                new CascadeFixtures.CountingBiblePublicationService([]),
                new MediaTestScopeFactory(options));

            await handler.HandleAsync(dispatcher);

            Assert.Empty(dispatcher.Dispatched);
        }
    }

    [Fact]
    public async Task HandleAsync_section_cascade_noop_when_get_publication_codes_throws()
    {
        var dispatcher = new RecordingDispatcher();
        var schedule = new ScheduleStateItem
        {
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "sjjc",
            BiblePublicationSectionCode = "1",
            BiblePublicationTrackCode = null,
        };
        var throwingBible = new UnexpectedBiblePublicationService();
        var state = new FakeApplicationState(new ApplicationState([], schedule));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new CascadeFixtures.SectionTracksMediaService("E", "sjjc", "1"),
            state,
            biblePublicationService: throwingBible,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());
        var handler = new BiblePublicationCascadeHandler(
            throwingBible,
            new CascadeFixtures.SectionTracksMediaService("E", "sjjc", "1"),
            new IdleLanguageContentService(),
            itemSelector,
            state,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        await handler.HandleAsync(dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }
}
