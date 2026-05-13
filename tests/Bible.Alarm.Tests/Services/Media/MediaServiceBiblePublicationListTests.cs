#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceBiblePublicationListTests
{
    static MediaServiceBiblePublicationListTests()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
    }

    private sealed class StubBiblePublicationService : IBiblePublicationService
    {
        private readonly List<string> available;
        private readonly IReadOnlyDictionary<string, BiblePublication> downloaded;

        public StubBiblePublicationService(
            List<string> available,
            IReadOnlyDictionary<string, BiblePublication>? downloaded = null)
        {
            this.available = available;
            this.downloaded = downloaded ?? new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode,
            string? categoryName = null, bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(downloaded, StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(available);

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode,
            CancellationToken cancellationToken = default) =>
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
    }

    private sealed class NopLanguageContentService : ILanguageContentService
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
            Task.FromResult(false);

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(string languageCode, string? categoryName = null,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode,
            string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    [Fact]
    public async Task GetBiblePublicationsAsync_adds_Dramas_placeholder_when_discovery_lists_it_and_db_has_dramas_row_without_language()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<MediaDbContext>(b =>
        {
            b.UseSqlite(connection);
            b.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            db.Database.EnsureCreated();

            var category = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryDramas };
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            db.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = "dramas",
                LanguageId = null,
                CategoryId = category.Id,
                Category = category,
                IsMusic = false,
                SectionLanguages = [],
            });
            await db.SaveChangesAsync();
        }

        var biblePub = new StubBiblePublicationService(
            new List<string> { AppConstants.Media.BiblePublicationCategoryDramas });

        var result = await MediaServiceBiblePublicationList.GetBiblePublicationsAsync(
            new GetBiblePublicationsServices(biblePub, new NopLanguageContentService(), scopeFactory),
            new GetBiblePublicationsOptions("E", DownloadAll: false));

        Assert.True(result.TryGetValue(AppConstants.Media.BiblePublicationCategoryDramas, out var placeholder));
        Assert.Equal(AppConstants.Media.BiblePublicationCategoryDramas, placeholder.PublicationCode);
    }
}
