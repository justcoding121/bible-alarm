#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationFetchCoordinatorTests
{
    private sealed class CoordinatorMediaStub : IMediaService
    {
        /// <summary>Language code filter for music-catalog snapshots served by <see cref="GetBiblePublications"/>.</summary>
        internal string ServeCatalogLanguageCode { get; init; } = string.Empty;

        internal Dictionary<string, BiblePublication>? ServeCatalog { get; init; }

        internal int ExpectedPublicationCountForLanguage { get; init; }

        internal string ExpectedCountLanguageCode { get; init; } = string.Empty;

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false)
        {
            if (ServeCatalog != null &&
                categoryName == AppConstants.Media.BiblePublicationCategoryMusic &&
                string.Equals(languageCode, ServeCatalogLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ServeCatalog);
            }

            return Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));
        }

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

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false)
        {
            if (ExpectedPublicationCountForLanguage > 0 &&
                string.Equals(languageCode, ExpectedCountLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ExpectedPublicationCountForLanguage);
            }

            return Task.FromResult(0);
        }

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private static BiblePublication CatalogedPublication(string code, int id = 1) =>
        new()
        {
            Id = id,
            PublicationCode = code,
            Name = $"Display name for {code}",
            Sections = [],
            Tracks = [],
        };

    [Fact]
    public async Task FetchMusicPublicationsAsync_returns_null_when_vocal_requires_language_but_language_missing()
    {
        var media = new CoordinatorMediaStub();
        var sut = new MusicPublicationFetchCoordinator(media);

        var result = await sut.FetchMusicPublicationsAsync(
            languageCode: string.Empty,
            current: new AlarmMusic { LanguageCode = "E", PublicationCode = "osg" },
            downloadAll: false,
            progress: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchMusicPublicationsAsync_melody_mode_uses_default_language_catalog()
    {
        var catalog = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
        {
            ["iam"] = CatalogedPublication("iam", id: 11),
        };

        var media = new CoordinatorMediaStub
        {
            ServeCatalogLanguageCode = AppConstants.Media.DefaultLanguageCode,
            ServeCatalog = catalog,
        };
        var sut = new MusicPublicationFetchCoordinator(media);

        var result = await sut.FetchMusicPublicationsAsync(
            languageCode: null,
            current: null,
            downloadAll: false,
            progress: null);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("Display name for iam", result["iam"].Name);
    }

    [Fact]
    public async Task FetchMusicPublicationsAsync_downloadAll_short_circuits_when_everything_already_cataloged()
    {
        var fullyCataloged = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
        {
            ["osg"] = CatalogedPublication("osg", id: 21),
            ["sjj"] = CatalogedPublication("sjj", id: 22),
        };

        var media = new CoordinatorMediaStub
        {
            ServeCatalogLanguageCode = "S",
            ServeCatalog = fullyCataloged,
            ExpectedPublicationCountForLanguage = 2,
            ExpectedCountLanguageCode = "S",
        };

        var sut = new MusicPublicationFetchCoordinator(media);

        var result = await sut.FetchMusicPublicationsAsync(
            languageCode: "S",
            current: new AlarmMusic { LanguageCode = "S", PublicationCode = "osg" },
            downloadAll: true,
            progress: null);

        Assert.Same(fullyCataloged, result);
    }
}
