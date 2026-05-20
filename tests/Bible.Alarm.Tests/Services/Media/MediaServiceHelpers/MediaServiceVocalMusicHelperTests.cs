#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Services.Media.MediaServiceHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceVocalMusicHelperTests
{
    private sealed class BiblePublicationCodesStub : IBiblePublicationService
    {
        private readonly List<string> codes;

        public BiblePublicationCodesStub(List<string> codes) => this.codes = codes;

        public void Dispose()
        {
        }

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default)
        {
            if (categoryName != AppConstants.Media.BiblePublicationCategoryMusic || !filterIsMusicWhenMusicCategory)
            {
                throw new Xunit.Sdk.XunitException("Unexpected bible catalog query.");
            }

            return Task.FromResult(codes.ToList());
        }

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());
    }

    private sealed class VocalMusicMapStub(Dictionary<string, VocalMusic> data) : IVocalMusicService
    {
        public void Dispose()
        {
        }

        public Task<VocalMusic?> GetByLanguageAndCodeAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(data.TryGetValue(publicationCode, out var v) ? v : null);

        public Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(data, StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static DbContextOptions<MediaDbContext> SqliteMemoryOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task GetReleasesAsync_adds_music_publication_with_null_language_from_catalog()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pubCode = "vm-no-lang-seed";

        await using (var seed = new MediaDbContext(options))
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            var bp = new BiblePublication
            {
                Name = "Instrumental-ish",
                PublicationCode = pubCode,
                LanguageId = null,
                IsVideo = false,
                IsMusic = true,
            };

            bp.BiblePublicationCategories.Add(new BiblePublicationCategory { BiblePublication = bp, Category = musicCat });

            seed.Categories.Add(musicCat);
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);
        var bible = new BiblePublicationCodesStub([pubCode]);

        var result = await MediaServiceVocalMusicHelper.GetReleasesAsync(bible, new VocalMusicMapStub([]),
            factory, "E",
            CancellationToken.None);

        var vm = Assert.Single(result.Values);
        Assert.Equal(pubCode, vm.Code);
        Assert.Null(vm.LanguageId);
        Assert.True(vm.Publication.IsMusic);
    }

    [Fact]
    public async Task GetReleasesAsync_merges_downloaded_releases_with_catalog_and_placeholders()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string downloadedCode = "vm-downloaded";
        const string placeholderCode = "vm-placeholder-from-pl";

        await using (var seed = new MediaDbContext(options))
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            seed.Categories.Add(musicCat);

            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = placeholderCode,
                Category = musicCat,
                CategoryId = musicCat.Id,
                LanguageId = lang.Id,
                Language = lang,
                IsMusic = true,
            });
            await seed.SaveChangesAsync();
        }

        var downloaded = new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase)
        {
            [downloadedCode] = new VocalMusic
            {
                Publication = new BiblePublication { PublicationCode = downloadedCode, Name = "DL", IsMusic = true },
            },
        };

        var factory = new MediaTestScopeFactory(options);
        var bible = new BiblePublicationCodesStub([downloadedCode, placeholderCode]);

        var result = await MediaServiceVocalMusicHelper.GetReleasesAsync(
            bible,
            new VocalMusicMapStub(downloaded),
            factory,
            "E",
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(downloadedCode));
        Assert.True(result.ContainsKey(placeholderCode));
    }

    [Fact]
    public async Task GetReleasesAsync_creates_placeholder_for_missing_music_code_from_publication_language()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pubCode = "vm-placeholder-from-pl";

        await using (var seed = new MediaDbContext(options))
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            seed.Categories.Add(musicCat);

            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            seed.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = pubCode,
                Category = musicCat,
                CategoryId = musicCat.Id,
                LanguageId = lang.Id,
                Language = lang,
                IsMusic = true,
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);
        var bible = new BiblePublicationCodesStub([pubCode]);

        var result = await MediaServiceVocalMusicHelper.GetReleasesAsync(bible, new VocalMusicMapStub([]),
            factory, "e",
            CancellationToken.None);

        Assert.True(result.TryGetValue(pubCode, out var vm));
        Assert.Equal(pubCode, vm.Publication.PublicationCode);
        Assert.Equal(pubCode, vm.Publication.Name);
        Assert.True(vm.Publication.IsMusic);
        Assert.NotNull(vm.Publication.Language);
        Assert.Equal("E", vm.Publication.Language!.LanguageCode);
    }
}
