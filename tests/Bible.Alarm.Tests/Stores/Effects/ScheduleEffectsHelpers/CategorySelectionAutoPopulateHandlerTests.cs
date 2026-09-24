#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Runtime.InteropServices;
using IDispatcher = Fluxor.IDispatcher;
using CascadeFixtures = Bible.Alarm.Tests.Support.CascadeHandlerTestFixtures;

namespace Bible.Alarm.Tests;

public sealed class CategorySelectionAutoPopulateHandlerTests
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

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
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

    private sealed class UnexpectedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Scope must not be created when CurrentSchedule is null.");
    }

    [Fact]
    public async Task HandleAsync_skips_when_current_schedule_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            new FakeApplicationState(new ApplicationState([])),
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var deps = new CategorySelectionAutoPopulateHandlerDeps(
            BiblePublicationService: new UnexpectedBiblePublicationService(),
            MediaService: new IdleMediaService(),
            LanguageContentService: new IdleLanguageContentService(),
            LanguageNameService: new IdleLanguageNameService(),
            ItemSelector: itemSelector,
            State: new FakeApplicationState(new ApplicationState([])),
            ScopeFactory: new UnexpectedScopeFactory(),
            Logger: TestLogging.CreateLogger());

        var sut = new CategorySelectionAutoPopulateHandler(deps);

        await sut.HandleAsync(new CategorySelectionAction(1, AppConstants.Media.BiblePublicationCategoryMusic), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleAsync_returns_when_no_languages_for_category()
    {
        var dispatcher = new RecordingDispatcher();
        var current = new ScheduleStateItem { Id = 1, Name = "Draft" };
        var itemSelector = new BiblePublicationSelectionItemSelector(
            new IdleMediaService(),
            new FakeApplicationState(new ApplicationState([], current)),
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnexpectedScopeFactory());

        var deps = new CategorySelectionAutoPopulateHandlerDeps(
            BiblePublicationService: new EmptyLanguagesBiblePublicationService(),
            MediaService: new IdleMediaService(),
            LanguageContentService: new IdleLanguageContentService(),
            LanguageNameService: new IdleLanguageNameService(),
            ItemSelector: itemSelector,
            State: new FakeApplicationState(new ApplicationState([], currentSchedule: current)),
            ScopeFactory: new UnexpectedScopeFactory(),
            Logger: TestLogging.CreateLogger());

        var sut = new CategorySelectionAutoPopulateHandler(deps);

        await sut.HandleAsync(new CategorySelectionAction(1, AppConstants.Media.BiblePublicationCategoryMusic), dispatcher);

        Assert.Empty(dispatcher.Dispatched);
    }

    private sealed class EmptyLanguagesBiblePublicationService : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private sealed class CategoryAutoPopulateBiblePublicationService(
        Dictionary<string, Language> languages,
        List<string> publicationCodes) : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(languages);

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(publicationCodes);

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(publicationCodes.FirstOrDefault());

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(publicationCodes);

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private sealed class CachedLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("English");

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(languageCode);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => "English";

        public string? GetNameByLanguageCodeCached(string languageCode) => languageCode;
    }

    private static CategorySelectionAutoPopulateHandler CreateHandler(
        ScheduleStateItem current,
        IBiblePublicationService biblePublicationService,
        IMediaService media,
        ILanguageContentService languageContent,
        IServiceScopeFactory scopeFactory,
        ILanguageNameService? languageNameService = null)
    {
        var state = new FakeApplicationState(new ApplicationState([], currentSchedule: current));
        var itemSelector = new BiblePublicationSelectionItemSelector(
            media,
            state,
            biblePublicationService: biblePublicationService,
            biblePublicationSectionService: null,
            languageContentService: languageContent,
            scopeFactory: scopeFactory);
        var deps = new CategorySelectionAutoPopulateHandlerDeps(
            BiblePublicationService: biblePublicationService,
            MediaService: media,
            LanguageContentService: languageContent,
            LanguageNameService: languageNameService ?? new IdleLanguageNameService(),
            ItemSelector: itemSelector,
            State: state,
            ScopeFactory: scopeFactory,
            Logger: TestLogging.CreateLogger());
        return new CategorySelectionAutoPopulateHandler(deps);
    }

    [Fact]
    public async Task HandleAsync_preserves_previous_language_when_available_in_category()
    {
        var english = new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var french = new Language { Id = 2, LanguageCode = "F", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = english,
            ["F"] = french,
        };
        const string pub = "sjjc";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedBibleFlatPublicationAsync(options, pub, "F");
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem { Id = 1, BiblePublicationLanguageCode = "F" };
            var scopeFactory = new MediaTestScopeFactory(options);
            var sut = CreateHandler(
                current,
                new CascadeFixtures.DbBackedBiblePublicationService(scopeFactory, languages, [pub]),
                new CascadeFixtures.FlatTracksMediaService("F", pub),
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                scopeFactory,
                new CachedLanguageNameService());

            await sut.HandleAsync(
                new CategorySelectionAction(10, AppConstants.Media.BiblePublicationCategoryBible, previousLanguageCode: "F"),
                dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal("F", update.Schedule.BiblePublicationLanguageCode);
            Assert.Equal(pub, update.Schedule.BiblePublicationCode);
        }
    }

    [Fact]
    public async Task HandleAsync_auto_populates_no_language_music_publication()
    {
        const string pub = "iam";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            const string section = "iam-1";
            await CascadeFixtures.SeedMusicNoLanguageSectionWithTrackAsync(options, pub, section, "1", "One");
            var english = new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase) { ["E"] = english };
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem { Id = 1 };
            using var progress = new CascadeFixtures.CategoryProgressRecordingRecipient();
            var scopeFactory = new MediaTestScopeFactory(options);
            var sut = CreateHandler(
                current,
                new CascadeFixtures.DbBackedBiblePublicationService(scopeFactory, languages, [pub]),
                new CascadeFixtures.SectionTracksMediaService("E", pub, section, withoutLanguage: true),
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                scopeFactory);

            await sut.HandleAsync(new CategorySelectionAction(5, AppConstants.Media.BiblePublicationCategoryMusic), dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal(pub, update.Schedule.BiblePublicationCode);
            Assert.Equal("1", update.Schedule.BiblePublicationTrackCode);
            Assert.Contains(progress.Received, p => p is { IsComplete: true, Progress: -1 });
        }
    }

    [Fact]
    public async Task HandleAsync_auto_populates_bible_publication_from_database()
    {
        const string pub = "sjjc";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedBibleFlatPublicationAsync(options, pub, "E");
            var english = new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase) { ["E"] = english };
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem { Id = 1 };
            var scopeFactory = new MediaTestScopeFactory(options);
            var sut = CreateHandler(
                current,
                new CascadeFixtures.DbBackedBiblePublicationService(scopeFactory, languages, [pub]),
                new CascadeFixtures.FlatTracksMediaService("E", pub),
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                scopeFactory,
                new CachedLanguageNameService());

            await sut.HandleAsync(new CategorySelectionAction(3, AppConstants.Media.BiblePublicationCategoryBible), dispatcher);

            var action = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(action);
            Assert.Equal(pub, update.Schedule.BiblePublicationCode);
            Assert.Equal("1", update.Schedule.BiblePublicationTrackCode);
            Assert.Equal("E", update.Schedule.BiblePublicationLanguageCode);
        }
    }

    [Fact]
    public async Task HandleAsync_reverts_schedule_on_network_error()
    {
        var english = new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase) { ["E"] = english };
        const string pub = "nwt";
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            await CascadeFixtures.SeedBiblePublicationLanguageOnlyAsync(options, pub, "E");
            var snapshot = new ScheduleStateItem { Id = 1, Name = "Before", BiblePublicationCategoryName = "Old" };
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem { Id = 1, Name = "During" };
            using var progress = new CascadeFixtures.CategoryProgressRecordingRecipient();
            var sut = CreateHandler(
                current,
                new CategoryAutoPopulateBiblePublicationService(languages, [pub]),
                new IdleMediaService(),
                new CascadeFixtures.ThrowingOnEnsureLanguageContentService(new HttpRequestException("offline")),
                new MediaTestScopeFactory(options));

            try
            {
                await sut.HandleAsync(
                    new CategorySelectionAction(7, AppConstants.Media.BiblePublicationCategoryBible, previousScheduleSnapshot: snapshot),
                    dispatcher);
            }
            catch (COMException)
            {
                return;
            }

            var revert = Assert.Single(dispatcher.Dispatched);
            var update = Assert.IsType<UpdateScheduleFromViewModelAction>(revert);
            Assert.Same(snapshot, update.Schedule);
            Assert.Contains(progress.Received, p => p is { HasError: true, IsComplete: true });
        }
    }

    [Fact]
    public async Task HandleAsync_returns_when_languages_exist_but_no_publication_resolves()
    {
        var english = new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase) { ["E"] = english };
        var (options, connection) = await CascadeFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            var dispatcher = new RecordingDispatcher();
            var current = new ScheduleStateItem { Id = 1 };
            var sut = CreateHandler(
                current,
                new CategoryAutoPopulateBiblePublicationService(languages, []),
                new IdleMediaService(),
                new CascadeFixtures.ConfigurableLanguageContentService(true),
                new MediaTestScopeFactory(options));

            await sut.HandleAsync(new CategorySelectionAction(2, AppConstants.Media.BiblePublicationCategoryBible), dispatcher);

            Assert.Empty(dispatcher.Dispatched);
        }
    }
}
