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

public sealed class LanguageContentPublicationTracksFetcherTests
{
    private const string TestLangFlat = "LCPT-F1";
    private const string TestLangNwt = "LCPT-N1";
    private const string TestLangMed = "LCPT-M1";

    private static string OsgMp3JsonForLanguage(string normalizedLangWritten) =>
        "{\"files\":{\"" + normalizedLangWritten + "\":{\"MP3\":[{\"track\":1,\"file\":{\"url\":\"https://cdn.example/osg-track.mp3\"},\"title\":\"One\"}]}},\"pubName\":\"OSG\"}";

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

    private static async Task SeedOsgCategoriesAsync(MediaDbContext db)
    {
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.MusicPublicationCodeOsg))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedDramasGoodNewsCategoriesAsync(MediaDbContext db)
    {
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(
                     AppConstants.Media.NormalizedPublicationCodeDramasGoodNews))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        await db.SaveChangesAsync();
    }

    private static LanguageContentPublicationTracksFetcher CreateSut(
        DbContextOptions<MediaDbContext> options,
        HttpMessageHandler handler,
        IInternetConnectivityChecker? connectivity = null)
    {
        var http = new HttpClient(handler);
        var logger = TestLogging.CreateLogger();
        return new LanguageContentPublicationTracksFetcher(
            new MediaTestScopeFactory(options),
            logger,
            new MediatorFetcher(http, logger),
            new FlatPublicationFetcher(http, logger, new VideoLocalizedNameFetcher(http, logger)),
            connectivity);
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ReturnsFalse_When_PublicationLanguage_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedOsgCategoriesAsync(seed);
                seed.Languages.Add(new Language
                {
                    LanguageCode = TestLangFlat,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                });
                await seed.SaveChangesAsync();
            }

            var sut = CreateSut(options, new JsonHandler("{}"));

            Assert.False(await sut.FetchPublicationTracksAsync(
                AppConstants.Media.MusicPublicationCodeOsg,
                TestLangFlat.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ReturnsFalse_When_PublicationLanguage_Has_Null_LanguageId()
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

            var sut = CreateSut(options, new JsonHandler("{}"));

            Assert.False(await sut.FetchPublicationTracksAsync(
                AppConstants.Media.MelodyMusicPublicationCodeIam,
                AppConstants.Media.DefaultLanguageCode));
        }
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ReturnsFalse_When_English_Template_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedOsgCategoriesAsync(seed);

                var musicCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic);

                var langFlat = new Language
                {
                    LanguageCode = TestLangFlat,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langFlat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
                    Language = langFlat,
                    LanguageId = langFlat.Id,
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    IsMusic = true,
                    CatalogType = CatalogType.Flat,
                });
                await seed.SaveChangesAsync();
            }

            var sut = CreateSut(options, new JsonHandler("{}"));

            Assert.False(await sut.FetchPublicationTracksAsync(
                AppConstants.Media.MusicPublicationCodeOsg,
                TestLangFlat.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ReturnsFalse_When_CatalogType_Is_Sectioned()
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
                var langT = new Language
                {
                    LanguageCode = TestLangNwt,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langE, langT);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    Language = langT,
                    LanguageId = langT.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    Name = "NWT EN",
                    PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
                    LanguageId = langE.Id,
                    Language = langE,
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

            var sut = CreateSut(options, new JsonHandler("{}"));

            Assert.False(await sut.FetchPublicationTracksAsync(
                AppConstants.Media.BiblePublicationCodeNwt,
                TestLangNwt.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ThrowsHttpRequest_When_Offline_And_Preconditions_Met_Flat()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedOsgCategoriesAsync(seed);

                var musicCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic);

                var langE = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langFlat = new Language
                {
                    LanguageCode = TestLangFlat,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langE, langFlat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
                    Language = langFlat,
                    LanguageId = langFlat.Id,
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    IsMusic = true,
                    CatalogType = CatalogType.Flat,
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
                    LanguageId = langE.Id,
                    Language = langE,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();
            }

            var sut = CreateSut(options, new JsonHandler("{}"), new FakeConnectivityChecker(available: false));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.FetchPublicationTracksAsync(
                    AppConstants.Media.MusicPublicationCodeOsg,
                    TestLangFlat.ToLowerInvariant()));
        }
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ReturnsTrue_For_Flat_Catalog_When_GetPub_Returns_Tracks()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedOsgCategoriesAsync(seed);

                var musicCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic);

                var langE = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langFlat = new Language
                {
                    LanguageCode = TestLangFlat,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langE, langFlat);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
                    Language = langFlat,
                    LanguageId = langFlat.Id,
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    IsMusic = true,
                    CatalogType = CatalogType.Flat,
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
                    LanguageId = langE.Id,
                    Language = langE,
                    IsMusic = true,
                    IsVideo = false,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();
            }

            var sut = CreateSut(options, new JsonHandler(OsgMp3JsonForLanguage(TestLangFlat)));

            Assert.True(await sut.FetchPublicationTracksAsync(
                AppConstants.Media.MusicPublicationCodeOsg,
                TestLangFlat.ToLowerInvariant()));

            await using var verify = new MediaDbContext(options);
            var localized = await verify.BiblePublications
                .Include(b => b.Language)
                .Include(b => b.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync(b =>
                    b.PublicationCode == AppConstants.Media.MusicPublicationCodeOsg &&
                    b.Language!.LanguageCode == TestLangFlat);

            Assert.Single(localized.Tracks);
            Assert.Contains("cdn.example", localized.Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ReturnsFalse_For_MediatorSectioned_When_Mediator_Response_Has_No_Tracks()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                await SeedDramasGoodNewsCategoriesAsync(seed);

                var dramaCat = await seed.Categories.SingleAsync(c =>
                    c.CategoryCode == AppConstants.Media.BiblePublicationCategoryDramas);

                var langE = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langMed = new Language
                {
                    LanguageCode = TestLangMed,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langE, langMed);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeDramasGoodNews,
                    Language = langMed,
                    LanguageId = langMed.Id,
                    Category = dramaCat,
                    CategoryId = dramaCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.MediatorSectioned,
                });

                seed.BiblePublications.Add(new BiblePublication
                {
                    Name = "Good News EN",
                    PublicationCode = AppConstants.Media.BiblePublicationCodeDramasGoodNews,
                    LanguageId = langE.Id,
                    Language = langE,
                    IsMusic = false,
                    IsVideo = true,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = dramaCat, CategoryId = dramaCat.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();
            }

            var sut = CreateSut(options, new JsonHandler("{}"));

            Assert.False(await sut.FetchPublicationTracksAsync(
                AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                TestLangMed.ToLowerInvariant()));

            await using var verify = new MediaDbContext(options);
            Assert.Equal(1, await verify.BiblePublications.CountAsync());
        }
    }
}
