#nullable enable

using System.Threading;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ScheduleDisplayNameBibleHelperTests
{
    private sealed class StubMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

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

    private sealed class StubLanguageNameService : ILanguageNameService
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

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class StubBiblePublicationService : IBiblePublicationService
    {
        private readonly Dictionary<string, Language> _languages;
        private readonly BiblePublication? _publication;

        public StubBiblePublicationService(
            Dictionary<string, Language>? languages = null,
            BiblePublication? publication = null)
        {
            _languages = languages ?? new Dictionary<string, Language>();
            _publication = publication;
        }

        public void Dispose() { }

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(_languages);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_publication);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_publication);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode) { }
    }

    private sealed class ThrowingBiblePublicationService : IBiblePublicationService
    {
        public void Dispose() { }

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated service failure");

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode) { }
    }

    [Fact]
    public async Task PopulateAsync_early_exit_when_publication_track_and_section_empty()
    {
        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: null,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = string.Empty,
            TrackCode = string.Empty,
            LanguageCode = null,
            SectionCode = null,
        };

        await sut.PopulateAsync(state, bible);

        Assert.Null(state.BiblePublicationSectionName);
    }

    [Fact]
    public async Task PopulateAsync_sets_language_direction_when_language_found()
    {
        var languages = new Dictionary<string, Language>
        {
            ["AR"] = new Language { Id = 1, LanguageCode = "AR", Direction = "rtl" }
        };
        var stubService = new StubBiblePublicationService(languages: languages);

        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: stubService,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = string.Empty,
            TrackCode = string.Empty,
            LanguageCode = "AR",
            SectionCode = null,
        };

        await sut.PopulateAsync(state, bible);

        Assert.Equal("rtl", state.BiblePublicationLanguageDirection);
    }

    [Fact]
    public async Task PopulateAsync_uses_language_code_as_name_fallback_when_language_not_in_dict()
    {
        var stubService = new StubBiblePublicationService(languages: new Dictionary<string, Language>());

        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: stubService,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = string.Empty,
            TrackCode = string.Empty,
            LanguageCode = "ZZ",
            SectionCode = null,
        };

        await sut.PopulateAsync(state, bible);

        Assert.Equal("ZZ", state.BiblePublicationLanguageName);
    }

    [Fact]
    public async Task PopulateAsync_applies_publication_code_as_name_when_service_returns_null_publication()
    {
        var stubService = new StubBiblePublicationService(publication: null);

        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: stubService,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            TrackCode = string.Empty,
            LanguageCode = "E",
            SectionCode = null,
        };

        await sut.PopulateAsync(state, bible);

        Assert.Equal("nwtsty", state.BiblePublicationName);
    }

    [Fact]
    public async Task PopulateAsync_skips_track_title_population_when_track_code_empty()
    {
        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: null,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            TrackCode = string.Empty,
            LanguageCode = "E",
            SectionCode = null,
        };

        await sut.PopulateAsync(state, bible);

        Assert.Null(state.BiblePublicationTrackTitle);
    }

    [Fact]
    public async Task PopulateAsync_sets_track_title_fallback_chapter_for_sectioned_publication()
    {
        var stubService = new StubBiblePublicationService(publication: null);

        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: stubService,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "nwtsty",
            TrackCode = "5",
            LanguageCode = "E",
            SectionCode = "1",
        };

        await sut.PopulateAsync(state, bible);

        Assert.Equal("Chapter 5", state.BiblePublicationTrackTitle);
    }

    [Fact]
    public async Task PopulateAsync_sets_track_title_fallback_track_for_non_sectioned_publication()
    {
        // "osg" is a vocal music publication — HasSectionStructure returns false
        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: null,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "osg",
            TrackCode = "7",
            LanguageCode = "E",
            SectionCode = null,
        };

        await sut.PopulateAsync(state, bible);

        Assert.Equal("Track 7", state.BiblePublicationTrackTitle);
    }

    [Fact]
    public async Task PopulateAsync_swallows_exception_from_IBiblePublicationService()
    {
        var throwingService = new ThrowingBiblePublicationService();

        var sut = new ScheduleDisplayNameBibleHelper(
            TestLogging.CreateLogger(),
            biblePublicationService: throwingService,
            new StubMediaService(),
            new StubLanguageNameService(),
            new EmptyServiceProvider());

        var state = new ScheduleStateItem { Id = 1, Name = "S" };
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = string.Empty,
            TrackCode = string.Empty,
            LanguageCode = "E",
            SectionCode = null,
        };

        await sut.PopulateAsync(state, bible);

        Assert.Equal("E", state.BiblePublicationLanguageName);
    }
}
