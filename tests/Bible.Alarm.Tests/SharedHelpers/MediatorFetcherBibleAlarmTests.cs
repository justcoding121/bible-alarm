#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class MediatorFetcherBibleAlarmTests
{
    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private static string MinimalDramasGoodNewsCategoryJson()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var nk = AppConstants.Media.PubMediaJson.NaturalKey;
        var pc = AppConstants.Media.PubMediaJson.PrimaryCategory;
        var files = AppConstants.Media.PubMediaJson.Files;
        var pdu = AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl;
        var title = AppConstants.Media.PubMediaJson.Title;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var dramaKey = AppConstants.Media.BiblePublicationCodeDramasGoodNews;

        return "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"Dramas cat\",\""
            + media
            + "\":[{\""
            + nk
            + "\":\"nk1\",\""
            + pc
            + "\":\""
            + dramaKey
            + "\",\""
            + files
            + "\":[{\""
            + pdu
            + "\":\"https://cdn/m.mp4\"}],\""
            + title
            + "\":\"Pilot ep\"}]}}";
    }

    private static string EmptyCategoryMediaJson()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var nm = AppConstants.Media.PubMediaJson.Name;

        return $"{{\"{cat}\":{{\"{nm}\":\"Only name\",\"{media}\":[]}}}}";
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

    [Fact]
    public void Constructor_Throws_When_HttpClientNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MediatorFetcher(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_Throws_When_LoggerNull()
    {
        using var http = new HttpClient(new JsonHandler("{}"));
        Assert.Throws<ArgumentNullException>(() => new MediatorFetcher(http, null!));
    }

    [Fact]
    public async Task FetchEnglishMediatorPublicationAsync_ReturnsFalse_When_Response_Has_No_Tracks()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using var db = new MediaDbContext(options);

            foreach (var code in JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.BiblePublicationCodeDramasGoodNews))
            {
                db.Categories.Add(new Category { CategoryCode = code });
            }

            var lang = new Language
            {
                LanguageCode = "Z9",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };
            db.Languages.Add(lang);
            await db.SaveChangesAsync();

            using var http = new HttpClient(new JsonHandler(EmptyCategoryMediaJson()));
            var sut = new MediatorFetcher(http, TestLogging.CreateLogger());

            Assert.False(await sut.FetchEnglishMediatorPublicationAsync(
                db,
                AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                "Z9",
                lang,
                CancellationToken.None));
            Assert.False(await db.BiblePublications.AnyAsync());
        }
    }

    [Fact]
    public async Task FetchEnglishMediatorPublicationAsync_Inserts_Publication_When_Valid_Response()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using var db = new MediaDbContext(options);

            foreach (var code in JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.BiblePublicationCodeDramasGoodNews))
            {
                db.Categories.Add(new Category { CategoryCode = code });
            }

            var lang = new Language
            {
                LanguageCode = AppConstants.Media.DefaultLanguageCode,
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };
            db.Languages.Add(lang);
            await db.SaveChangesAsync();

            using var http = new HttpClient(new JsonHandler(MinimalDramasGoodNewsCategoryJson()));
            var sut = new MediatorFetcher(http, TestLogging.CreateLogger());

            Assert.True(await sut.FetchEnglishMediatorPublicationAsync(
                db,
                AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                AppConstants.Media.DefaultLanguageCode,
                lang,
                CancellationToken.None));

            var publication = await db.BiblePublications
                .Include(p => p.Tracks)
                .SingleAsync();
            Assert.Contains(AppConstants.Media.BiblePublicationCodeDramasGoodNews, publication.PublicationCode, StringComparison.OrdinalIgnoreCase);
            var track = Assert.Single(publication.Tracks);
            Assert.Equal("1", track.TrackCode);
            Assert.Contains("Pilot", track.Title, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchMediatorPublicationTracksAsync_ReturnsFalse_When_Target_Language_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                foreach (var code in JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.BiblePublicationCodeDramasGoodNews))
                {
                    seed.Categories.Add(new Category { CategoryCode = code });
                }

                var langEnglish = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langEnglish);
                await seed.SaveChangesAsync();

                var dramaCategory = seed.Categories.First();
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeDramasGoodNews,
                    Name = "English dramas",
                    LanguageId = langEnglish.Id,
                    Language = langEnglish,
                    IsMusic = false,
                    IsVideo = true,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = dramaCategory, CategoryId = dramaCategory.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();

                await using var read = new MediaDbContext(options);
                var englishPub = await read.BiblePublications
                    .Include(bp => bp.BiblePublicationCategories)
                    .ThenInclude(bpc => bpc.Category)
                    .SingleAsync(bp => bp.Language != null &&
                        bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

                using var http = new HttpClient(new JsonHandler(MinimalDramasGoodNewsCategoryJson()));
                var sut = new MediatorFetcher(http, TestLogging.CreateLogger());

                Assert.False(await sut.FetchMediatorPublicationTracksAsync(
                    read,
                    AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                    "XX",
                    englishPub,
                    CancellationToken.None));
            }

            await using var verify = new MediaDbContext(options);
            Assert.Equal(1, await verify.BiblePublications.CountAsync());
        }
    }

    [Fact]
    public async Task FetchMediatorPublicationTracksAsync_ReturnsFalse_When_English_Template_Has_No_Category()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                var langEnglish = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langEnglish);
                var langMx = new Language
                {
                    LanguageCode = "MX",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langMx);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeDramasGoodNews,
                    Name = "Bare",
                    LanguageId = langEnglish.Id,
                    Language = langEnglish,
                    IsMusic = false,
                    IsVideo = true,
                    BiblePublicationCategories = [],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();

                await using var read = new MediaDbContext(options);
                var englishPub = await read.BiblePublications
                    .Include(bp => bp.BiblePublicationCategories)
                    .ThenInclude(bpc => bpc.Category)
                    .SingleAsync();

                using var http = new HttpClient(new JsonHandler(MinimalDramasGoodNewsCategoryJson()));
                var sut = new MediatorFetcher(http, TestLogging.CreateLogger());

                Assert.False(await sut.FetchMediatorPublicationTracksAsync(
                    read,
                    AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                    "MX",
                    englishPub,
                    CancellationToken.None));
            }
        }
    }

    [Fact]
    public async Task FetchMediatorPublicationTracksAsync_ReturnsFalse_When_Mediator_Returns_No_Tracks()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                foreach (var code in JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.BiblePublicationCodeDramasGoodNews))
                {
                    seed.Categories.Add(new Category { CategoryCode = code });
                }

                var langEnglish = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langMx = new Language
                {
                    LanguageCode = "MX",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langEnglish);
                seed.Languages.Add(langMx);
                await seed.SaveChangesAsync();

                var dramaCategory = seed.Categories.First();
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeDramasGoodNews,
                    Name = "English dramas",
                    LanguageId = langEnglish.Id,
                    Language = langEnglish,
                    IsMusic = false,
                    IsVideo = true,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = dramaCategory, CategoryId = dramaCategory.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();

                await using var read = new MediaDbContext(options);
                var englishPub = await read.BiblePublications
                    .Include(bp => bp.BiblePublicationCategories)
                    .ThenInclude(bpc => bpc.Category)
                    .SingleAsync(bp => bp.Language != null &&
                        bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

                using var http = new HttpClient(new JsonHandler(EmptyCategoryMediaJson()));
                var sut = new MediatorFetcher(http, TestLogging.CreateLogger());

                Assert.False(await sut.FetchMediatorPublicationTracksAsync(
                    read,
                    AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                    "MX",
                    englishPub,
                    CancellationToken.None));
            }
        }
    }

    [Fact]
    public async Task FetchMediatorPublicationTracksAsync_ReturnsTrue_And_Upserts_For_Target_Language()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                foreach (var code in JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.BiblePublicationCodeDramasGoodNews))
                {
                    seed.Categories.Add(new Category { CategoryCode = code });
                }

                var langEnglish = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langEnglish);
                var langMx = new Language
                {
                    LanguageCode = "MX",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langMx);
                await seed.SaveChangesAsync();

                var dramaCategory = seed.Categories.First();
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = AppConstants.Media.BiblePublicationCodeDramasGoodNews,
                    Name = "English dramas template",
                    LanguageId = langEnglish.Id,
                    Language = langEnglish,
                    IsMusic = false,
                    IsVideo = true,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = dramaCategory, CategoryId = dramaCategory.Id },
                    ],
                    Sections = [],
                    Tracks = [],
                });
                await seed.SaveChangesAsync();

                await using var read = new MediaDbContext(options);
                var englishPub = await read.BiblePublications
                    .Include(bp => bp.BiblePublicationCategories)
                    .ThenInclude(bpc => bpc.Category)
                    .SingleAsync(bp => bp.Language != null &&
                        bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

                using var http = new HttpClient(new JsonHandler(MinimalDramasGoodNewsCategoryJson()));
                var sut = new MediatorFetcher(http, TestLogging.CreateLogger());

                Assert.True(await sut.FetchMediatorPublicationTracksAsync(
                    read,
                    AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                    "MX",
                    englishPub,
                    CancellationToken.None));
            }

            await using var verify = new MediaDbContext(options);
            Assert.Equal(2, await verify.BiblePublications.CountAsync());
            var localized = await verify.BiblePublications
                .Include(p => p.Language)
                .Include(p => p.Tracks)
                .SingleAsync(p => p.Language != null && p.Language.LanguageCode == "MX");
            var track = Assert.Single(localized.Tracks);
            Assert.Equal("1", track.TrackCode);
        }
    }
}
