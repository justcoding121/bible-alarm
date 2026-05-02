#nullable enable

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
}
