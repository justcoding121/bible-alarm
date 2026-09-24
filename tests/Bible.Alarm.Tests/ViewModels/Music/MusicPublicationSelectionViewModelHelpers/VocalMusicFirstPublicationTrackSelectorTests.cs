#nullable enable

using System.Net.Http;
using System.Reflection;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class VocalMusicFirstPublicationTrackSelectorTests
{
    private const string TestPubCode = "osg";

    private sealed class VocalSelectorMediaStub : IdleCatalogMediaService
    {
        internal Dictionary<string, BiblePublication>? Publications { get; init; }

        internal SortedDictionary<string, BiblePublicationTrack>? Tracks { get; init; }

        public override Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode,
            string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null,
            bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(Publications ?? new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public override Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode,
            string versionCode, string? sectionCode) =>
            Task.FromResult(Tracks ?? new SortedDictionary<string, BiblePublicationTrack>(StringComparer.Ordinal));
    }

    private sealed class CodesBiblePublicationService(List<string> codes) : IBiblePublicationService
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
            Task.FromResult(codes);

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(codes.FirstOrDefault());

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(codes);

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private static LanguageListViewItemModel Language(string code) =>
        new(new Language { LanguageCode = code, Direction = AppConstants.Media.TextDirectionLeftToRight }, code);

    private static object? InvokeStatic(string name, params object?[] args)
    {
        var method = typeof(VocalMusicFirstPublicationTrackSelector).GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method!.Invoke(null, args);
    }

    private static async Task InvokeCascadeDownloadAsync(
        VocalMusicFirstPublicationTrackSelector sut,
        LanguageListViewItemModel language,
        string publicationCode,
        IFetchProgress? progress = null)
    {
        var method = typeof(VocalMusicFirstPublicationTrackSelector).GetMethod(
            "CascadeDownloadFirstVocalPublicationWhenNeededAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(sut, [language, publicationCode, progress])!;
        await task;
    }

    [Fact]
    public void Ctor_accepts_service_slots()
    {
        var sut = new VocalMusicFirstPublicationTrackSelector(
            null!,
            biblePublicationService: null,
            languageContentService: null,
            scopeFactory: null);
        Assert.NotNull(sut);
    }

    [Fact]
    public async Task GetFirstSongPublicationAndTrackForLanguageAsync_returns_empty_when_bible_publication_service_missing()
    {
        var sut = new VocalMusicFirstPublicationTrackSelector(
            new IdleCatalogMediaService(),
            biblePublicationService: null,
            languageContentService: null,
            scopeFactory: new MediaTestScopeFactory(
                (await CascadeHandlerTestFixtures.CreateEmptyMediaDbAsync()).Options));

        var (pub, trackCode, trackName, pubName) =
            await sut.GetFirstSongPublicationAndTrackForLanguageAsync(Language("S"), currentSchedule: null);

        Assert.Null(pub);
        Assert.Empty(trackCode);
        Assert.Empty(trackName);
        Assert.Empty(pubName);
    }

    [Fact]
    public async Task GetFirstSongPublicationAndTrackForLanguageAsync_returns_empty_when_scope_factory_missing()
    {
        var sut = new VocalMusicFirstPublicationTrackSelector(
            new IdleCatalogMediaService(),
            new CodesBiblePublicationService([TestPubCode]),
            languageContentService: null,
            scopeFactory: null);

        var (pub, trackCode, _, _) =
            await sut.GetFirstSongPublicationAndTrackForLanguageAsync(Language("S"), currentSchedule: null);

        Assert.Null(pub);
        Assert.Empty(trackCode);
    }

    [Fact]
    public async Task GetFirstSongPublicationAndTrackForLanguageAsync_returns_empty_when_no_vocal_publication_codes()
    {
        var (options, connection) = await CascadeHandlerTestFixtures.CreateEmptyMediaDbAsync();
        await using (connection)
        {
            var sut = new VocalMusicFirstPublicationTrackSelector(
                new IdleCatalogMediaService(),
                new CodesBiblePublicationService([]),
                languageContentService: null,
                new MediaTestScopeFactory(options));

            var (pub, trackCode, _, _) =
                await sut.GetFirstSongPublicationAndTrackForLanguageAsync(Language("S"), currentSchedule: null);

            Assert.Null(pub);
            Assert.Empty(trackCode);
        }
    }

    [Fact]
    public async Task CascadeDownloadFirstVocalPublicationWhenNeededAsync_skips_ensure_for_english()
    {
        var languageContent = new CascadeHandlerTestFixtures.ConfigurableLanguageContentService(ensureExists: true);
        var sut = new VocalMusicFirstPublicationTrackSelector(
            new IdleCatalogMediaService(),
            biblePublicationService: null,
            languageContent,
            scopeFactory: null);

        await InvokeCascadeDownloadAsync(
            sut,
            Language(AppConstants.Media.DefaultLanguageCode),
            TestPubCode);

        Assert.Empty(languageContent.EnsureCalls);
    }

    [Fact]
    public async Task CascadeDownloadFirstVocalPublicationWhenNeededAsync_calls_ensure_for_non_english()
    {
        var languageContent = new CascadeHandlerTestFixtures.ConfigurableLanguageContentService(ensureExists: true);
        var sut = new VocalMusicFirstPublicationTrackSelector(
            new IdleCatalogMediaService(),
            biblePublicationService: null,
            languageContent,
            scopeFactory: null);

        await InvokeCascadeDownloadAsync(sut, Language("S"), TestPubCode);

        Assert.Contains((TestPubCode, "S"), languageContent.EnsureCalls);
    }

    [Fact]
    public async Task CascadeDownloadFirstVocalPublicationWhenNeededAsync_rethrows_network_failures()
    {
        var sut = new VocalMusicFirstPublicationTrackSelector(
            new IdleCatalogMediaService(),
            biblePublicationService: null,
            new ThrowingLanguageContentService(new HttpRequestException("offline")),
            scopeFactory: null);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            InvokeCascadeDownloadAsync(sut, Language("S"), TestPubCode));
    }

    [Fact]
    public void PickFirstCatalogedSongPublication_prefers_matching_code_when_cataloged()
    {
        var publications = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
        {
            [TestPubCode] = new BiblePublication
            {
                Id = 3,
                PublicationCode = TestPubCode,
                Name = "Sing Out",
                LanguageId = 1,
            },
            ["other"] = new BiblePublication
            {
                Id = 1,
                PublicationCode = "other",
                Name = "other",
                LanguageId = 1,
            },
        };

        var picked = InvokeStatic("PickFirstCatalogedSongPublication", publications, TestPubCode) as BiblePublication;

        Assert.NotNull(picked);
        Assert.Equal(TestPubCode, picked!.PublicationCode, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PickVocalTrackForCascade_reuses_persisted_track_when_same_selection()
    {
        var tracks = new SortedDictionary<string, BiblePublicationTrack>
        {
            ["3"] = new BiblePublicationTrack { TrackCode = "3", Title = "Song Three" },
            ["9"] = new BiblePublicationTrack { TrackCode = "9", Title = "Song Nine" },
        };
        var schedule = new ScheduleStateItem
        {
            MusicLanguageCode = "S",
            MusicPublicationCode = TestPubCode,
            MusicTrackCode = "3",
        };

        var result = InvokeStatic(
            "PickVocalTrackForCascade",
            schedule,
            "S",
            TestPubCode,
            tracks) as ValueTuple<string, string>?;

        Assert.Equal("3", result?.Item1);
        Assert.Equal("Song Three", result?.Item2);
    }

    [Fact]
    public void IsSameLanguageAndSongPublication_true_when_schedule_matches()
    {
        var schedule = new ScheduleStateItem
        {
            MusicLanguageCode = "S",
            MusicPublicationCode = TestPubCode,
            MusicTrackCode = "1",
        };

        var same = InvokeStatic("IsSameLanguageAndSongPublication", schedule, "S", TestPubCode);

        Assert.True(same is bool b && b);
    }

    private sealed class ThrowingLanguageContentService(Exception ex) : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchPublicationSectionsAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchSectionTracksAsync(string publicationCode, string sectionCode, string languageCode,
            bool replaceExistingTracksFromApi = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsurePublicationExistsAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            throw ex;

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(string languageCode, string? categoryName = null,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
