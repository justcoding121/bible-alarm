#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageContentSectionTracksFetcherTests
{
    private const string TestLangSt = "LCST-01";

    private static string SectionTracksJson(string normalizedLangWritten) =>
        "{\"files\":{\"" + normalizedLangWritten + "\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/track.mp3\"},\"track\":1,\"title\":\"Vers\"}]}},\"pubName\":\"NWT\"}";

    private sealed class FakeConnectivityChecker(bool available) : IInternetConnectivityChecker
    {
        public Task<bool> IsInternetAvailableAsync() => Task.FromResult(available);
    }

    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private static async Task<(SqliteConnection Connection, DbContextOptions<MediaDbContext> Options)> CreateConnectionAndOptionsAsync()
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

        return (connection, options);
    }

    private static async Task SeedNwtCategoriesAsync(MediaDbContext db)
    {
        var pubCode = AppConstants.Media.BiblePublicationCodeNwt;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        await db.SaveChangesAsync();
    }

    private static BiblePublicationSection AddSection(MediaDbContext db, BiblePublication pub, string sectionCode, string sectionName)
    {
        var section = new BiblePublicationSection
        {
            Name = sectionName,
            SectionCode = sectionCode,
            BiblePublicationId = pub.Id,
            BiblePublication = pub,
            Tracks = [],
        };
        db.BiblePublicationSections.Add(section);
        return section;
    }

    /// <summary>
    /// Seeds NWT bible category, <paramref name="langCode"/> language row, PublicationLanguage, optional empty-shell publication with <paramref name="sectionStorageCode"/> section, and SectionLanguage for <paramref name="sectionAvailabilityCode"/> (lowercase).
    /// </summary>
    private static async Task SeedPublicationSectionAndAvailabilityAsync(
        MediaDbContext db,
        string langCode,
        string sectionStorageCode,
        string sectionAvailabilityCode,
        bool includeSectionInPublication)
    {
        await SeedNwtCategoriesAsync(db);
        var bibleCat = await db.Categories.SingleAsync(c =>
            c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

        var lang = new Language
        {
            LanguageCode = langCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var pl = new PublicationLanguage
        {
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            Language = lang,
            LanguageId = lang.Id,
            Category = bibleCat,
            CategoryId = bibleCat.Id,
            IsMusic = false,
            CatalogType = CatalogType.Sectioned,
        };
        db.PublicationLanguages.Add(pl);

        var pub = new BiblePublication
        {
            Name = "NWT " + langCode,
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            LanguageId = lang.Id,
            Language = lang,
            IsMusic = false,
            IsVideo = false,
            CatalogType = CatalogType.Sectioned,
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
            ],
            Sections = [],
            Tracks = [],
        };
        db.BiblePublications.Add(pub);
        await db.SaveChangesAsync();

        if (includeSectionInPublication)
        {
            AddSection(db, pub, sectionStorageCode, "Matthew");
            await db.SaveChangesAsync();
        }

        db.SectionLanguages.Add(new SectionLanguage
        {
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            SectionCode = sectionAvailabilityCode,
            Language = lang,
            LanguageId = lang.Id,
            PublicationLanguage = pl,
            PublicationLanguageId = pl.Id,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_Localized_Publication_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                seed.Languages.Add(new Language
                {
                    LanguageCode = TestLangSt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                });
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentSectionTracksFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.False(await sut.FetchSectionTracksAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                "mat",
                TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_Section_Absent_From_Publication()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedNwtCategoriesAsync(seed);
                var bibleCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

                var lang = new Language
                {
                    LanguageCode = TestLangSt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    Language = lang,
                    LanguageId = lang.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    Name = "NWT ST shell",
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    LanguageId = lang.Id,
                    Language = lang,
                    IsMusic = false,
                    IsVideo = false,
                    CatalogType = CatalogType.Sectioned,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentSectionTracksFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.False(await sut.FetchSectionTracksAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                "mat",
                TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_SectionLanguage_Row_Absent()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedNwtCategoriesAsync(seed);
                var bibleCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

                var lang = new Language
                {
                    LanguageCode = TestLangSt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    Language = lang,
                    LanguageId = lang.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                });

                var pub = new BiblePublication
                {
                    Name = "NWT ST",
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    LanguageId = lang.Id,
                    Language = lang,
                    IsMusic = false,
                    IsVideo = false,
                    CatalogType = CatalogType.Sectioned,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                };
                seed.BiblePublications.Add(pub);
                await seed.SaveChangesAsync();
                AddSection(seed, pub, "mat", "Matthew");
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentSectionTracksFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.False(await sut.FetchSectionTracksAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                "mat",
                TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_Uses_CaseInsensitive_Section_Code_When_Exact_Key_Mismatch()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedPublicationSectionAndAvailabilityAsync(
                    seed,
                    TestLangSt,
                    sectionStorageCode: "MAT",
                    sectionAvailabilityCode: "mat",
                    includeSectionInPublication: true);
            }

            using var http = new HttpClient(new JsonHandler(SectionTracksJson(TestLangSt)));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentSectionTracksFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.True(await sut.FetchSectionTracksAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                "mat",
                TestLangSt.ToLowerInvariant()));

            await using var verify = new MediaDbContext(options);
            var section = await verify.BiblePublicationSections
                .Include(s => s.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync(s =>
                    s.SectionCode == "MAT");

            Assert.Single(section.Tracks);
            Assert.Contains("cdn.example", section.Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ThrowsHttpRequest_When_Offline_And_Preconditions_Met()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedPublicationSectionAndAvailabilityAsync(
                    seed,
                    TestLangSt,
                    sectionStorageCode: "mat",
                    sectionAvailabilityCode: "mat",
                    includeSectionInPublication: true);
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentSectionTracksFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger),
                new FakeConnectivityChecker(available: false));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.FetchSectionTracksAsync(
                    AppConstants.Media.BiblePublicationCodeNwt,
                    "mat",
                    TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsTrue_When_Http_Has_Tracks_For_Language()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedPublicationSectionAndAvailabilityAsync(
                    seed,
                    TestLangSt,
                    sectionStorageCode: "mat",
                    sectionAvailabilityCode: "mat",
                    includeSectionInPublication: true);
            }

            using var http = new HttpClient(new JsonHandler(SectionTracksJson(TestLangSt)));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentSectionTracksFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.True(await sut.FetchSectionTracksAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                "mat",
                TestLangSt.ToLowerInvariant()));

            await using var verify = new MediaDbContext(options);
            var bp = await verify.BiblePublications
                .Include(b => b.Sections)
                .ThenInclude(s => s.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync(b =>
                    b.PublicationCode == AppConstants.Media.BiblePublicationCodeNwt &&
                    b.Language != null &&
                    b.Language.LanguageCode == TestLangSt);

            var section = bp.Sections.Single(s => s.SectionCode.Equals("mat", StringComparison.OrdinalIgnoreCase));
            Assert.Single(section.Tracks);
            Assert.Equal("1", section.Tracks[0].TrackCode);
        }
    }
}
