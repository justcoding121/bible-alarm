#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class EnglishContentSeederTests
{
    private static EnglishContentSeeder CreateSut(MediaTestScopeFactory factory, HttpClient httpClient) =>
        new(
            factory,
            httpClient,
            TestLogging.CreateLogger(),
            new MediatorFetcher(httpClient, TestLogging.CreateLogger()),
            new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger())));

    private static async Task<(MediaTestScopeFactory Factory, SqliteConnection Connection)> CreateFactoryAsync()
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

        return (new MediaTestScopeFactory(options), connection);
    }

    [Fact]
    public void Constructor_Throws_When_ScopeFactory_Null()
    {
        using var httpClient = new HttpClient();
        Assert.Throws<ArgumentNullException>(() =>
            new EnglishContentSeeder(
                null!,
                httpClient,
                TestLogging.CreateLogger(),
                new MediatorFetcher(httpClient, TestLogging.CreateLogger()),
                new FlatPublicationFetcher(
                    httpClient,
                    TestLogging.CreateLogger(),
                    new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()))));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        var (factory, connection) = CreateFactorySync();
        using (connection)
        {
            using var httpClient = new HttpClient();
            Assert.Throws<ArgumentNullException>(() =>
                new EnglishContentSeeder(
                    factory,
                    httpClient,
                    null!,
                    new MediatorFetcher(httpClient, TestLogging.CreateLogger()),
                    new FlatPublicationFetcher(
                        httpClient,
                        TestLogging.CreateLogger(),
                        new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()))));
        }
    }

    [Fact]
    public void Constructor_Throws_When_MediatorFetcher_Null()
    {
        var (factory, connection) = CreateFactorySync();
        using (connection)
        {
            using var httpClient = new HttpClient();
            Assert.Throws<ArgumentNullException>(() =>
                new EnglishContentSeeder(factory, httpClient, TestLogging.CreateLogger(), null!, new FlatPublicationFetcher(
                    httpClient,
                    TestLogging.CreateLogger(),
                    new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()))));
        }
    }

    [Fact]
    public void Constructor_Throws_When_FlatPublicationFetcher_Null()
    {
        var (factory, connection) = CreateFactorySync();
        using (connection)
        {
            using var httpClient = new HttpClient();
            Assert.Throws<ArgumentNullException>(() =>
                new EnglishContentSeeder(
                    factory,
                    httpClient,
                    TestLogging.CreateLogger(),
                    new MediatorFetcher(httpClient, TestLogging.CreateLogger()),
                    null!));
        }
    }

    private static (MediaTestScopeFactory Factory, SqliteConnection Connection) CreateFactorySync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var init = new MediaDbContext(options))
        {
            init.Database.EnsureCreated();
        }

        return (new MediaTestScopeFactory(options), connection);
    }

    [Fact]
    public async Task Seed_Returns_False_When_English_Language_Row_Missing()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            using var httpClient = new HttpClient();
            var sut = CreateSut(factory, httpClient);

            Assert.False(await sut.SeedEnglishPublicationAsync("nwt"));
        }
    }

    [Fact]
    public async Task Seed_Returns_True_When_Magazine_Has_No_English_SectionLanguages()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            using var httpClient = new HttpClient();

            var year = Math.Min(DateTime.UtcNow.Year, MagazineHelper.MagazineEndYear - 1);
            var magCode = $"w{year}";
            var sut = CreateSut(factory, httpClient);

            Assert.True(await sut.SeedEnglishPublicationAsync(magCode));
        }
    }

    [Fact]
    public async Task Seed_Returns_False_When_Publication_Has_No_Language_Rows()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                seed.Categories.Add(cat);
                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "iam-no-english-lang",
                    Name = "Melody",
                    LanguageId = null,
                    Language = null,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = cat, CategoryId = cat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = true
                });
                await seed.SaveChangesAsync();
            }

            using var httpClient = new HttpClient();
            var sut = CreateSut(factory, httpClient);

            Assert.False(await sut.SeedEnglishPublicationAsync("iam-no-english-lang"));
        }
    }

    [Fact]
    public async Task Seed_Returns_True_When_English_Publication_Already_Exists()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                var bibleCatCode = JwSourceHelper.GetCategoryCode("nwt")!;
                var bibleCat = new Category { CategoryCode = bibleCatCode };
                var lang = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight
                };
                seed.Categories.Add(bibleCat);
                seed.Languages.Add(lang);
                await seed.SaveChangesAsync();

                seed.BiblePublications.Add(new BiblePublication
                {
                    PublicationCode = "nwt",
                    Name = "NWT",
                    Language = lang,
                    LanguageId = lang.Id,
                    CatalogType = CatalogType.Sectioned,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = bibleCat, CategoryId = bibleCat.Id }
                    ],
                    Sections = [],
                    Tracks = [],
                    IsVideo = false,
                    IsMusic = false
                });
                await seed.SaveChangesAsync();
            }

            using var httpClient = new HttpClient();
            var sut = CreateSut(factory, httpClient);

            Assert.True(await sut.SeedEnglishPublicationAsync("nwt"));
        }
    }

    [Fact]
    public async Task Seed_Returns_False_When_Publication_Code_Has_No_Known_Category()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                seed.Languages.Add(new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                });
                await seed.SaveChangesAsync();
            }

            using var httpClient = new HttpClient();
            var sut = CreateSut(factory, httpClient);

            Assert.False(await sut.SeedEnglishPublicationAsync("zzz_non_cataloged_publication_xyz"));
        }
    }

    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static async Task SeedOsgCategoriesAndEnglishLanguageAsync(MediaDbContext db)
    {
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.MusicPublicationCodeOsg))
        {
            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        db.Languages.Add(new Language
        {
            LanguageCode = AppConstants.Media.DefaultLanguageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDramasCategoriesAndEnglishLanguageAsync(MediaDbContext db)
    {
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(
                     AppConstants.Media.NormalizedPublicationCodeDramasGoodNews))
        {
            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        db.Languages.Add(new Language
        {
            LanguageCode = AppConstants.Media.DefaultLanguageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        });

        await db.SaveChangesAsync();
    }

    private const string OsgFlatJson =
        "{\"files\":{\"E\":{\"MP3\":[{\"track\":1,\"file\":{\"url\":\"https://cdn.example/osg.mp3\"},\"title\":\"Song One\"}]}},\"pubName\":\"Sing Out\"}";

    private static string MediatorDramaJson()
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
               + "\":\"Dramas show\",\""
               + media
               + "\":[{\""
               + nk
               + "\":\"k1\",\""
               + pc
               + "\":\""
               + dramaKey
               + "\",\""
               + files
               + "\":[{\""
               + pdu
               + "\":\"https://cdn/mediator/track.mp4\"}],\""
               + title
               + "\":\"Pilot\"}]}}";
    }

    [Fact]
    public async Task Seed_Persists_Flat_Osg_Publication_With_Tracks()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedOsgCategoriesAndEnglishLanguageAsync(seed);
            }

            using var http = new HttpClient(new JsonHandler(OsgFlatJson));
            var sut = CreateSut(factory, http);

            Assert.True(await sut.SeedEnglishPublicationAsync(AppConstants.Media.MusicPublicationCodeOsg));

            await using var verify = new MediaDbContext(opts);
            var pub = await verify.BiblePublications
                .Include(p => p.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync(p => p.PublicationCode == AppConstants.Media.MusicPublicationCodeOsg);
            Assert.Single(pub.Tracks);
            Assert.NotNull(pub.Tracks[0].TrackUrl);
        }
    }

    [Fact]
    public async Task Seed_Returns_False_When_Flat_Osg_Http_Fails()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedOsgCategoriesAndEnglishLanguageAsync(seed);
            }

            using var http = new HttpClient(new NotFoundHandler());
            var sut = CreateSut(factory, http);

            Assert.False(await sut.SeedEnglishPublicationAsync(AppConstants.Media.MusicPublicationCodeOsg));
        }
    }

    [Fact]
    public async Task Seed_Persists_Mediator_Drama_Publication_With_Tracks()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedDramasCategoriesAndEnglishLanguageAsync(seed);
            }

            using var http = new HttpClient(new JsonHandler(MediatorDramaJson()));
            var sut = CreateSut(factory, http);

            Assert.True(await sut.SeedEnglishPublicationAsync(AppConstants.Media.BiblePublicationCodeDramasGoodNews));

            await using var verify = new MediaDbContext(opts);
            var pub = await verify.BiblePublications
                .Include(p => p.Tracks)
                .SingleAsync(p =>
                    p.PublicationCode == AppConstants.Media.BiblePublicationCodeDramasGoodNews);
            Assert.NotEmpty(pub.Tracks);
        }
    }

    [Fact]
    public async Task Seed_Returns_False_When_Category_Row_Missing_For_Known_Publication()
    {
        var (factory, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                seed.Languages.Add(new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                });
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler(OsgFlatJson));
            var sut = CreateSut(factory, http);

            Assert.False(await sut.SeedEnglishPublicationAsync(AppConstants.Media.MusicPublicationCodeOsg));
        }
    }
}
