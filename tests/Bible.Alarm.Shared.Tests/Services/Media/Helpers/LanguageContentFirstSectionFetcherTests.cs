#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageContentFirstSectionFetcherTests
{
    private const string PublicationCodeShortcut = "lcfs-shortcut-w1";

    private sealed class JsonHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
    }

    private sealed class NoOpLanguageContentService : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchPublicationSectionsAsync(string publicationCode, string languageCode, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchSectionTracksAsync(string publicationCode, string sectionCode, string languageCode, bool replaceExistingTracksFromApi = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsurePublicationExistsAsync(string publicationCode, string languageCode, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(string languageCode, string? categoryName = null, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode, IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode, string languageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
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
    public async Task Constructor_Throws_When_ScopeFactoryNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new JsonHandler());
            var sectionFetcher = new SectionFetcher(http, TestLogging.CreateLogger());
            var noOp = new NoOpLanguageContentService();

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentFirstSectionFetcher(null!, TestLogging.CreateLogger(), noOp, sectionFetcher));
        }
    }

    [Fact]
    public async Task Constructor_Throws_When_LoggerNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new JsonHandler());
            var sectionFetcher = new SectionFetcher(http, TestLogging.CreateLogger());
            var factory = new MediaTestScopeFactory(options);
            var noOp = new NoOpLanguageContentService();

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentFirstSectionFetcher(factory, null!, noOp, sectionFetcher));
        }
    }

    [Fact]
    public async Task Constructor_Throws_When_LanguageContentServiceNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new JsonHandler());
            var sectionFetcher = new SectionFetcher(http, TestLogging.CreateLogger());
            var factory = new MediaTestScopeFactory(options);

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentFirstSectionFetcher(factory, TestLogging.CreateLogger(), null!, sectionFetcher));
        }
    }

    [Fact]
    public async Task Constructor_Throws_When_SectionFetcherNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            var factory = new MediaTestScopeFactory(options);
            var noOp = new NoOpLanguageContentService();

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentFirstSectionFetcher(factory, TestLogging.CreateLogger(), noOp, null!));
        }
    }

    [Fact]
    public async Task FetchFirstSectionOnlyAsync_ReturnsFalse_When_PublicationLanguage_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new JsonHandler());
            var factory = new MediaTestScopeFactory(options);
            var sectionFetcher = new SectionFetcher(http, TestLogging.CreateLogger());
            var noOp = new NoOpLanguageContentService();
            var sut = new LanguageContentFirstSectionFetcher(factory, TestLogging.CreateLogger(), noOp, sectionFetcher);

            Assert.False(await sut.FetchFirstSectionOnlyAsync(
                PublicationCodeShortcut,
                "mat",
                "W9"));
        }
    }

    [Fact]
    public async Task FetchFirstSectionOnlyAsync_ReturnsFalse_When_English_Template_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                var bibleCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                seed.Categories.Add(bibleCat);

                var langMx = new Language
                {
                    LanguageCode = "W9",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.Add(langMx);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = PublicationCodeShortcut,
                    Language = langMx,
                    LanguageId = langMx.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                });
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler());
            var factory = new MediaTestScopeFactory(options);
            var sectionFetcher = new SectionFetcher(http, TestLogging.CreateLogger());
            var noOp = new NoOpLanguageContentService();
            var sut = new LanguageContentFirstSectionFetcher(factory, TestLogging.CreateLogger(), noOp, sectionFetcher);

            Assert.False(await sut.FetchFirstSectionOnlyAsync(PublicationCodeShortcut, "mat", "w9"));
        }
    }

    [Fact]
    public async Task FetchFirstSectionOnlyAsync_ReturnsTrue_When_LocalPublication_Already_Has_First_Section()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var seed = new MediaDbContext(options))
            {
                var bibleCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible };
                seed.Categories.Add(bibleCat);

                var langEnglish = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                var langMx = new Language
                {
                    LanguageCode = "W9",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                };
                seed.Languages.AddRange(langEnglish, langMx);
                await seed.SaveChangesAsync();

                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = PublicationCodeShortcut,
                    Language = langMx,
                    LanguageId = langMx.Id,
                    Category = bibleCat,
                    CategoryId = bibleCat.Id,
                    IsMusic = false,
                    CatalogType = CatalogType.Sectioned,
                });

                var englishPub = new BiblePublication
                {
                    Name = "Shortcut EN",
                    PublicationCode = PublicationCodeShortcut,
                    LanguageId = langEnglish.Id,
                    Language = langEnglish,
                    IsMusic = false,
                    IsVideo = false,
                    Sections = [],
                    Tracks = [],
                };
                var enSection = new BiblePublicationSection
                {
                    Name = "Matthew",
                    SectionCode = "mat",
                    BiblePublication = englishPub,
                    Tracks = [],
                };
                englishPub.Sections.Add(enSection);

                var mxPub = new BiblePublication
                {
                    Name = "Shortcut MX",
                    PublicationCode = PublicationCodeShortcut,
                    LanguageId = langMx.Id,
                    Language = langMx,
                    IsMusic = false,
                    IsVideo = false,
                    Sections = [],
                    Tracks = [],
                };
                var mxSection = new BiblePublicationSection
                {
                    Name = "Matthew",
                    SectionCode = "mat",
                    BiblePublication = mxPub,
                    Tracks = [],
                };
                mxPub.Sections.Add(mxSection);

                seed.BiblePublications.AddRange(englishPub, mxPub);
                await seed.SaveChangesAsync();
            }

            using var http = new HttpClient(new JsonHandler());
            var factory = new MediaTestScopeFactory(options);
            var sectionFetcher = new SectionFetcher(http, TestLogging.CreateLogger());
            var noOp = new NoOpLanguageContentService();
            var sut = new LanguageContentFirstSectionFetcher(factory, TestLogging.CreateLogger(), noOp, sectionFetcher);

            Assert.True(await sut.FetchFirstSectionOnlyAsync(PublicationCodeShortcut, "MAT", "w9"));
        }
    }
}
