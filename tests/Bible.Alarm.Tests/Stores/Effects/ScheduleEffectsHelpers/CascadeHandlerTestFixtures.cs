#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Messages.CategoryProgress;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests.Support;

/// <summary>
/// Shared hand-rolled stubs and SQLite seed helpers for ScheduleEffects cascade handler tests.
/// </summary>
internal static class CascadeHandlerTestFixtures
{
    internal sealed class ConfigurableLanguageContentService(bool ensureExists) : ILanguageContentService
    {
        public List<(string PublicationCode, string LanguageCode)> EnsureCalls { get; } = [];

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
            CancellationToken cancellationToken = default)
        {
            EnsureCalls.Add((publicationCode, languageCode));
            return Task.FromResult(ensureExists);
        }

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

    internal sealed class ThrowingOnEnsureLanguageContentService(Exception ex) : ILanguageContentService
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
            Task.FromException<bool>(ex);

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

    internal sealed class FlatTracksMediaService(string languageCode, string publicationCode, bool withoutLanguage = false) : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string lc, string versionCode, string? sectionCode)
        {
            var langOk = withoutLanguage
                ? string.IsNullOrEmpty(lc)
                : string.Equals(lc, languageCode, StringComparison.OrdinalIgnoreCase);
            if (langOk
                && string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase)
                && sectionCode == null)
            {
                return Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>(StringComparer.Ordinal)
                {
                    ["10"] = new BiblePublicationTrack { TrackCode = "10", Title = "Ten" },
                    ["2"] = new BiblePublicationTrack { TrackCode = "2", Title = "Two" },
                });
            }

            return Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());
        }

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string lc, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string lc, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string pub) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string lc, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string lc, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string lc, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string lc, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string lc, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string lc, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string lc, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(withoutLanguage);

        public Task<int> GetExpectedSectionCountAsync(string lc, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string lc, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    internal sealed class SectionTracksMediaService(string languageCode, string publicationCode, string sectionCode, bool withoutLanguage = false)
        : IMediaService
    {
        private readonly FlatTracksMediaService flatFallback = new(languageCode, publicationCode, withoutLanguage);

        public void Dispose() => flatFallback.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            flatFallback.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string lc, string versionCode, string? sec)
        {
            var langOk = withoutLanguage
                ? string.IsNullOrEmpty(lc)
                : string.Equals(lc, languageCode, StringComparison.OrdinalIgnoreCase);
            if (langOk
                && string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(sec, sectionCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>(StringComparer.Ordinal)
                {
                    ["5"] = new BiblePublicationTrack { TrackCode = "5", Title = "Five" },
                    ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "One" },
                });
            }

            return flatFallback.GetBiblePublicationTracks(lc, versionCode, sec);
        }

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string lc, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            flatFallback.GetBiblePublications(lc, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string lc, string versionCode,
            IFetchProgress? progress = null)
        {
            if (!withoutLanguage
                && string.Equals(lc, languageCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.Ordinal)
                {
                    [sectionCode] = new BiblePublicationSection { SectionCode = sectionCode, Name = "Disc 1" },
                });
            }

            return flatFallback.GetBiblePublicationSections(lc, versionCode, progress);
        }

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string pub)
        {
            if (withoutLanguage && string.Equals(pub, publicationCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.Ordinal)
                {
                    [sectionCode] = new BiblePublicationSection { SectionCode = sectionCode, Name = "Disc 1" },
                });
            }

            return flatFallback.GetSectionsForPublicationWithoutLanguage(pub);
        }

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string lc, string versionCode, string sectionCode) =>
            flatFallback.GetBiblePublicationSection(lc, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string lc, string versionCode, string? sectionCode, string trackCode) =>
            flatFallback.GetBiblePublicationTrack(lc, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => flatFallback.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            flatFallback.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            flatFallback.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => flatFallback.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string lc, bool downloadAll = false) =>
            flatFallback.GetVocalMusicReleases(lc, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string lc, string publicationCode) =>
            flatFallback.GetVocalMusicTracks(lc, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(string lc, string versionCode, string? sectionCode, string trackCode, string url) =>
            flatFallback.UpdateBiblePublicationTrackUrl(lc, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string lc, string publicationCode, string trackCode, string url) =>
            flatFallback.UpdateVocalTrackUrl(lc, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            flatFallback.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            flatFallback.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string lc, string? categoryName = null) =>
            flatFallback.InvalidateBiblePublicationsCache(lc, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            flatFallback.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string lc, string publicationCode) =>
            flatFallback.GetExpectedSectionCountAsync(lc, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string lc, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            flatFallback.GetExpectedPublicationCountAsync(lc, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            flatFallback.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }

    internal sealed class DbBackedBiblePublicationService(
        IServiceScopeFactory scopeFactory,
        Dictionary<string, Language> languages,
        List<string> publicationCodes) : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public async Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            return await db.BiblePublications
                .AsNoTracking()
                .Include(x => x.Sections)
                .Where(x => x.PublicationCode == publicationCode &&
                            x.Language != null &&
                            x.Language.LanguageCode == languageCode.ToUpperInvariant())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            return await db.BiblePublications
                .AsNoTracking()
                .Include(x => x.Tracks)
                .Where(x => x.PublicationCode == publicationCode &&
                            x.Language != null &&
                            x.Language.LanguageCode == languageCode.ToUpperInvariant())
                .FirstOrDefaultAsync(cancellationToken);
        }

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

    internal sealed class CountingBiblePublicationService(List<string> publicationCodes) : IBiblePublicationService
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

    internal sealed class LanguagesBiblePublicationService(Dictionary<string, Language> languages) : IBiblePublicationService
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

    internal sealed class CategoryProgressRecordingRecipient : IRecipient<CategoryFetchProgressMessage>, IDisposable
    {
        public List<CategoryFetchProgress> Received { get; } = [];

        public CategoryProgressRecordingRecipient()
        {
            WeakReferenceMessenger.Default.Register<CategoryFetchProgressMessage>(this);
        }

        public void Receive(CategoryFetchProgressMessage message) => Received.Add(message.Value);

        public void Dispose() => WeakReferenceMessenger.Default.Unregister<CategoryFetchProgressMessage>(this);
    }

    internal static async Task<(DbContextOptions<MediaDbContext> Options, SqliteConnection Connection)> CreateEmptyMediaDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        return (options, connection);
    }

    internal static async Task SeedMusicFlatPublicationAsync(DbContextOptions<MediaDbContext> options, string publicationCode, string name)
    {
        await using var seed = new MediaDbContext(options);
        var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        var bp = new BiblePublication
        {
            Name = name,
            PublicationCode = publicationCode,
            LanguageId = null,
            IsVideo = false,
            IsMusic = true,
            CatalogType = CatalogType.Flat,
        };
        bp.BiblePublicationCategories.Add(new BiblePublicationCategory { BiblePublication = bp, Category = musicCat });
        seed.Categories.Add(musicCat);
        seed.BiblePublications.Add(bp);
        await seed.SaveChangesAsync();
    }

    internal static async Task SeedMusicLanguageBoundPublicationAsync(
        DbContextOptions<MediaDbContext> options,
        string publicationCode,
        string languageCode,
        string publicationName,
        bool includePublicationRow = true)
    {
        await using var seed = new MediaDbContext(options);
        var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        var lang = new Language { LanguageCode = languageCode, Direction = AppConstants.Media.TextDirectionLeftToRight };
        seed.Categories.Add(musicCat);
        seed.Languages.Add(lang);
        await seed.SaveChangesAsync();

        seed.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = publicationCode,
            Category = musicCat,
            CategoryId = musicCat.Id,
            LanguageId = lang.Id,
            Language = lang,
            IsMusic = true,
        });

        if (includePublicationRow)
        {
            var bp = new BiblePublication
            {
                Name = publicationName,
                PublicationCode = publicationCode,
                LanguageId = lang.Id,
                Language = lang,
                IsVideo = false,
                IsMusic = true,
                CatalogType = CatalogType.Flat,
            };
            bp.BiblePublicationCategories.Add(new BiblePublicationCategory { BiblePublication = bp, Category = musicCat });
            seed.BiblePublications.Add(bp);
        }

        await seed.SaveChangesAsync();
    }

    internal static async Task SeedBiblePublicationLanguageOnlyAsync(
        DbContextOptions<MediaDbContext> options,
        string publicationCode,
        string languageCode,
        string categoryCode = AppConstants.Media.BiblePublicationCategoryBible)
    {
        await using var seed = new MediaDbContext(options);
        var category = new Category { CategoryCode = categoryCode };
        var lang = new Language { LanguageCode = languageCode, Direction = AppConstants.Media.TextDirectionLeftToRight };
        seed.Categories.Add(category);
        seed.Languages.Add(lang);
        await seed.SaveChangesAsync();

        seed.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = publicationCode,
            Category = category,
            Language = lang,
            CatalogType = CatalogType.Flat,
        });
        await seed.SaveChangesAsync();
    }

    internal static async Task SeedBibleFlatPublicationAsync(
        DbContextOptions<MediaDbContext> options,
        string publicationCode,
        string languageCode,
        string categoryCode = AppConstants.Media.BiblePublicationCategoryBible)
    {
        await using var seed = new MediaDbContext(options);
        var category = new Category { CategoryCode = categoryCode };
        var lang = new Language { LanguageCode = languageCode, Direction = AppConstants.Media.TextDirectionLeftToRight };
        seed.Categories.Add(category);
        seed.Languages.Add(lang);
        await seed.SaveChangesAsync();

        seed.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = publicationCode,
            Category = category,
            Language = lang,
            CatalogType = CatalogType.Flat,
        });

        var bp = new BiblePublication
        {
            Name = publicationCode.ToUpperInvariant(),
            PublicationCode = publicationCode,
            LanguageId = lang.Id,
            Language = lang,
            IsVideo = false,
            IsMusic = false,
            CatalogType = CatalogType.Flat,
        };
        bp.BiblePublicationCategories.Add(new BiblePublicationCategory { BiblePublication = bp, Category = category });
        bp.Tracks.Add(new BiblePublicationTrack { TrackCode = "1", Title = "First", Publication = bp });
        seed.BiblePublications.Add(bp);
        await seed.SaveChangesAsync();
    }

    internal static async Task SeedMusicNoLanguageSectionWithTrackAsync(
        DbContextOptions<MediaDbContext> options,
        string publicationCode,
        string sectionCode,
        string trackCode,
        string trackTitle)
    {
        await using var seed = new MediaDbContext(options);
        var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        seed.Categories.Add(musicCat);
        var bp = new BiblePublication
        {
            Name = "Melody",
            PublicationCode = publicationCode,
            LanguageId = null,
            IsVideo = false,
            IsMusic = true,
            CatalogType = CatalogType.Sectioned,
        };
        bp.BiblePublicationCategories.Add(new BiblePublicationCategory { BiblePublication = bp, Category = musicCat });
        var section = new BiblePublicationSection
        {
            SectionCode = sectionCode,
            Name = "Disc 1",
            BiblePublication = bp,
        };
        bp.Sections.Add(section);
        section.Tracks.Add(new BiblePublicationTrack
        {
            TrackCode = trackCode,
            Title = trackTitle,
            Section = section,
            Publication = bp,
        });
        seed.BiblePublications.Add(bp);
        await seed.SaveChangesAsync();
    }

    internal static async Task SeedMusicSectionedPublicationAsync(
        DbContextOptions<MediaDbContext> options,
        string publicationCode,
        bool withoutLanguage)
    {
        await using var seed = new MediaDbContext(options);
        var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        seed.Categories.Add(musicCat);
        Language? lang = null;
        if (!withoutLanguage)
        {
            lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Languages.Add(lang);
        }

        await seed.SaveChangesAsync();

        var bp = new BiblePublication
        {
            Name = "Melody",
            PublicationCode = publicationCode,
            LanguageId = lang?.Id,
            Language = lang,
            IsVideo = false,
            IsMusic = true,
            CatalogType = CatalogType.Sectioned,
        };
        bp.BiblePublicationCategories.Add(new BiblePublicationCategory { BiblePublication = bp, Category = musicCat });
        seed.BiblePublications.Add(bp);
        await seed.SaveChangesAsync();
    }
}
