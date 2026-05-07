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

public sealed class LanguageContentPublicationSectionsFetcherTests
{
    private static string MinimalBiblePubMediaJsonForLanguage(string normalizedLanguageWritten) =>
        "{\"files\":{\"" + normalizedLanguageWritten + "\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/t.mp3\"},\"track\":1,\"title\":\"Matt\"}]}},\"pubName\":\"NWT\"}";

    private const string TestLangSt = "LCPS-ST";

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

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_PublicationLanguage_Missing()
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
            var sut = new LanguageContentPublicationSectionsFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.False(await sut.FetchPublicationSectionsAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_PublicationLanguage_Has_Null_LanguageId()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                seed.Categories.Add(musicCat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    LanguageId = null,
                    Language = null,
                    CategoryId = musicCat.Id,
                    Category = musicCat,
                    IsMusic = true,
                    CatalogType = CatalogType.Sectioned,
                });
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentPublicationSectionsFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.False(await sut.FetchPublicationSectionsAsync(
                AppConstants.Media.MelodyMusicPublicationCodeIam,
                AppConstants.Media.DefaultLanguageCode));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_English_Template_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedNwtCategoriesAsync(seed);
                var bibleCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

                var langSt = new Language
                {
                    LanguageCode = TestLangSt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langSt);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    Language = langSt,
                    LanguageId = langSt.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                });
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentPublicationSectionsFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.False(await sut.FetchPublicationSectionsAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_No_SectionLanguages_Rows()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedNwtCategoriesAsync(seed);
                var bibleCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

                var langE = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langSt = new Language
                {
                    LanguageCode = TestLangSt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langE, langSt);
                await seed.SaveChangesAsync();

                var pl = new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    Language = langSt,
                    LanguageId = langSt.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                };
                seed.PublicationLanguages.Add(pl);

                var englishPub = new BiblePublication
                {
                    Name = "NWT EN",
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    LanguageId = langE.Id,
                    Language = langE,
                    IsMusic = false,
                    IsVideo = false,
                    Sections = [],
                    Tracks = [],
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                    ],
                };
                seed.BiblePublications.Add(englishPub);
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentPublicationSectionsFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.False(await sut.FetchPublicationSectionsAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ThrowsHttpRequest_When_Offline_And_Preconditions_Met()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedNwtCategoriesAsync(seed);
                var bibleCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

                var langE = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langSt = new Language
                {
                    LanguageCode = TestLangSt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langE, langSt);
                await seed.SaveChangesAsync();

                var pl = new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    Language = langSt,
                    LanguageId = langSt.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                };
                seed.PublicationLanguages.Add(pl);

                seed.SectionLanguages.Add(new SectionLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    SectionCode = "mat",
                    Language = langSt,
                    LanguageId = langSt.Id,
                    PublicationLanguage = pl,
                });

                var englishPub = new BiblePublication
                {
                    Name = "NWT EN",
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    LanguageId = langE.Id,
                    Language = langE,
                    IsMusic = false,
                    IsVideo = false,
                    Sections = [],
                    Tracks = [],
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                    ],
                };
                seed.BiblePublications.Add(englishPub);
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentPublicationSectionsFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger),
                new FakeConnectivityChecker(available: false));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.FetchPublicationSectionsAsync(
                    AppConstants.Media.BiblePublicationCodeNwt,
                    TestLangSt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsTrue_When_SectionLanguages_And_Http_Return_Body()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedNwtCategoriesAsync(seed);
                var bibleCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible);

                var langE = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langSt = new Language
                {
                    LanguageCode = TestLangSt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langE, langSt);
                await seed.SaveChangesAsync();

                var pl = new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    Language = langSt,
                    LanguageId = langSt.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                };
                seed.PublicationLanguages.Add(pl);

                seed.SectionLanguages.Add(new SectionLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    SectionCode = "mat",
                    Language = langSt,
                    LanguageId = langSt.Id,
                    PublicationLanguage = pl,
                });

                var englishPub = new BiblePublication
                {
                    Name = "NWT EN",
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    LanguageId = langE.Id,
                    Language = langE,
                    IsMusic = false,
                    IsVideo = false,
                    Sections = [],
                    Tracks = [],
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id },
                    ],
                };
                seed.BiblePublications.Add(englishPub);
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler(MinimalBiblePubMediaJsonForLanguage(TestLangSt)));
            var logger = TestLogging.CreateLogger();
            var sut = new LanguageContentPublicationSectionsFetcher(
                new MediaTestScopeFactory(options),
                logger,
                new SectionFetcher(http, logger));

            Assert.True(await sut.FetchPublicationSectionsAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                TestLangSt.ToLowerInvariant()));

            await using var verify = new MediaDbContext(options);
            var bp = await verify.BiblePublications
                .Include(b => b.Language)
                .Include(b => b.Sections)
                .ThenInclude(s => s.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync(b =>
                    b.PublicationCode == AppConstants.Media.BiblePublicationCodeNwt &&
                    b.Language!.LanguageCode == TestLangSt);
            Assert.Single(bp.Sections);
            Assert.Equal("mat", bp.Sections[0].SectionCode, StringComparer.OrdinalIgnoreCase);
            Assert.Single(bp.Sections[0].Tracks);
        }
    }
}
