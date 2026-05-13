#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceTests
{
    private sealed class IdleMediaIndexService : IMediaIndexService
    {
        public string IndexRoot => string.Empty;
        public bool WasIndexReplacedThisRun => false;

        public void Dispose()
        {
        }

        public Task MigrateNonEnglishDataIfNeededAsync() => Task.CompletedTask;

        public Task Verify() => Task.CompletedTask;
    }

    private sealed class EmptyDistinctLanguagesBiblePublicationService : IBiblePublicationService
    {
        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(
            string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class UnusedBiblePublicationSectionService : IBiblePublicationSectionService
    {
        public Task<string?> GetSectionNameAsync(string languageCode, string publicationCode, string sectionCode,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<string, BiblePublicationSection>>
            GetSectionsByPublicationWithoutLanguageAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<BiblePublicationSection?> GetSectionAsync(
            string languageCode, string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedBiblePublicationTrackService : IBiblePublicationTrackService
    {
        public Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksBySectionAsync(
            string languageCode, string publicationCode, string? sectionCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<BiblePublicationTrack?> GetTrackAsync(string languageCode, string publicationCode, string? sectionCode,
            string trackCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string? sectionCode, string trackCode,
            string url, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedMelodyMusicService : IMelodyMusicService
    {
        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(
            string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(
            string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedVocalMusicService : IVocalMusicService
    {
        public Task<VocalMusic?> GetByLanguageAndCodeAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(
            string languageCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(
            string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public void Dispose()
        {
        }
    }

    private sealed class UnusedLanguageContentService : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(
            string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchPublicationTracksAsync(
            string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchPublicationSectionsAsync(
            string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchSectionTracksAsync(
            string publicationCode, string sectionCode, string languageCode,
            bool replaceExistingTracksFromApi = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> EnsurePublicationExistsAsync(
            string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchFirstPublicationForLanguageAsync(
            string languageCode, string? categoryName = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> EnsureAllPublicationsForLanguageAsync(
            string languageCode, string? categoryName = null, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> EnsureAllSectionsForPublicationAsync(
            string publicationCode, string languageCode, IFetchProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> FetchFirstSectionOnlyAsync(
            string publicationCode, string firstSectionCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
    }

    private sealed class IdleScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Scope not used by GetVocalMusicLanguages");
    }

    private static MediaService CreateSutForVocalLanguages(EmptyDistinctLanguagesBiblePublicationService biblePubs) =>
        new(new MediaServiceDependencies(
            new IdleMediaIndexService(),
            biblePubs,
            new UnusedBiblePublicationSectionService(),
            new UnusedBiblePublicationTrackService(),
            new UnusedMelodyMusicService(),
            new UnusedVocalMusicService(),
            new UnusedLanguageContentService(),
            new IdleScopeFactory()));

    [Fact]
    public async Task IsPublicationWithoutLanguageAsync_returns_false_when_code_is_whitespace()
    {
        var sut = CreateSutForVocalLanguages(new EmptyDistinctLanguagesBiblePublicationService());

        var result = await sut.IsPublicationWithoutLanguageAsync("   ");

        Assert.False(result);
    }

    [Fact]
    public async Task GetVocalMusicLanguages_adds_default_english_when_distinct_list_is_empty()
    {
        var sut = CreateSutForVocalLanguages(new EmptyDistinctLanguagesBiblePublicationService());

        var langs = await sut.GetVocalMusicLanguages();

        Assert.True(langs.ContainsKey(AppConstants.Media.DefaultLanguageCode));
        Assert.Equal(AppConstants.Media.DefaultLanguageCode, langs[AppConstants.Media.DefaultLanguageCode].LanguageCode);
    }
}
