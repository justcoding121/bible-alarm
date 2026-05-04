#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FlatPublicationFetcherTests
{
    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static async Task<(SqliteConnection Connection, MediaDbContext Db)> CreateDbAsync()
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

        var db = new MediaDbContext(options);
        return (connection, db);
    }

    private static async Task SeedOsgCategoriesAndLanguageAsync(MediaDbContext db)
    {
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication("osg"))
        {
            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        db.Languages.Add(new Language
        {
            LanguageCode = AppConstants.Media.DefaultLanguageCode,
            Direction = AppConstants.Media.TextDirectionLeftToRight
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public void Constructor_Throws_When_HttpClient_Null()
    {
        var vid = new VideoLocalizedNameFetcher(new HttpClient(new JsonHandler("{}")), TestLogging.CreateLogger());
        Assert.Throws<ArgumentNullException>(() =>
            new FlatPublicationFetcher(null!, TestLogging.CreateLogger(), vid));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        using var client = new HttpClient(new JsonHandler("{}"));
        var vid = new VideoLocalizedNameFetcher(client, TestLogging.CreateLogger());
        Assert.Throws<ArgumentNullException>(() => new FlatPublicationFetcher(client, null!, vid));
    }

    [Fact]
    public void Constructor_Throws_When_VideoLocalizedNameFetcher_Null()
    {
        using var client = new HttpClient(new JsonHandler("{}"));
        Assert.Throws<ArgumentNullException>(() =>
            new FlatPublicationFetcher(client, TestLogging.CreateLogger(), null!));
    }

    [Fact]
    public async Task Fetch_ReturnsFalse_When_No_Tracks_From_Api()
    {
        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            await SeedOsgCategoriesAndLanguageAsync(db);
            var lang = await db.Languages.SingleAsync();

            using var handler = new NotFoundHandler();
            using var httpClient = new HttpClient(handler);
            var sut = new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

            var englishStub = new BiblePublication
            {
                Name = "Songs",
                PublicationCode = "osg",
                IsVideo = false,
                IsMusic = true,
                Sections = [],
                Tracks = []
            };

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = englishStub,
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                Language = lang,
                CancellationToken = CancellationToken.None
            };

            Assert.False(await sut.FetchFlatPublicationTracksAsync(req));
            Assert.Equal(0, await db.BiblePublications.CountAsync());
        }
    }

    [Fact]
    public async Task Fetch_Inserts_Publication_When_Valid_Flat_Json()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"track\":1,\"file\":{\"url\":\"https://cdn.example/osg.mp3\"},\"title\":\"Song One\"}]}},\"pubName\":\"Sing&nbsp;Out\"}";

        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            await SeedOsgCategoriesAndLanguageAsync(db);
            var lang = await db.Languages.SingleAsync();

            using var handler = new JsonHandler(json);
            using var httpClient = new HttpClient(handler);
            var sut = new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

            var englishStub = new BiblePublication
            {
                Name = "Songs EN",
                PublicationCode = "osg",
                IsVideo = false,
                IsMusic = true,
                Sections = [],
                Tracks = []
            };

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = englishStub,
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                Language = lang,
                CancellationToken = CancellationToken.None
            };

            Assert.True(await sut.FetchFlatPublicationTracksAsync(req));

            var pub = await db.BiblePublications
                .Include(p => p.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync();

            Assert.Equal("osg", pub.PublicationCode);
            Assert.Single(pub.Tracks);
            Assert.Equal("1", pub.Tracks[0].TrackCode);
            Assert.Contains("cdn.example", pub.Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Fetch_Replaces_Tracks_When_Publication_Already_Exists_In_Database()
    {
        const string jsonFirst =
            "{\"files\":{\"E\":{\"MP3\":[{\"track\":1,\"file\":{\"url\":\"https://cdn/update-first.mp3\"},\"title\":\"One\"}]}},\"pubName\":\"First&nbsp;Name\"}";
        const string jsonSecond =
            "{\"files\":{\"E\":{\"MP3\":[{\"track\":9,\"file\":{\"url\":\"https://cdn/update-second.mp3\"},\"title\":\"Nine\"}]}},\"pubName\":\"Second&nbsp;Name\"}";

        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            await SeedOsgCategoriesAndLanguageAsync(db);
            var lang = await db.Languages.SingleAsync();

            using (var handler = new JsonHandler(jsonFirst))
            using (var httpClient = new HttpClient(handler))
            {
                var sut = new FlatPublicationFetcher(
                    httpClient,
                    TestLogging.CreateLogger(),
                    new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

                var englishStub = new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = "osg",
                    IsVideo = false,
                    IsMusic = true,
                    Sections = [],
                    Tracks = []
                };

                var req = new FetchFlatPublicationTracksRequest
                {
                    Db = db,
                    NormalizedPublicationCode = "osg",
                    NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                    EnglishPublication = englishStub,
                    IsVideo = false,
                    IsMusic = true,
                    FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                    Language = lang,
                    CancellationToken = CancellationToken.None
                };

                Assert.True(await sut.FetchFlatPublicationTracksAsync(req));
            }

            using (var handlerSecond = new JsonHandler(jsonSecond))
            using (var httpSecond = new HttpClient(handlerSecond))
            {
                var sutSecond = new FlatPublicationFetcher(
                    httpSecond,
                    TestLogging.CreateLogger(),
                    new VideoLocalizedNameFetcher(httpSecond, TestLogging.CreateLogger()));

                var reqSecond = new FetchFlatPublicationTracksRequest
                {
                    Db = db,
                    NormalizedPublicationCode = "osg",
                    NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                    EnglishPublication = new BiblePublication
                    {
                        Name = "Songs EN v2",
                        PublicationCode = "osg",
                        IsVideo = false,
                        IsMusic = true,
                        Sections = [],
                        Tracks = []
                    },
                    IsVideo = false,
                    IsMusic = true,
                    FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                    Language = lang,
                    CancellationToken = CancellationToken.None
                };

                Assert.True(await sutSecond.FetchFlatPublicationTracksAsync(reqSecond));
            }

            var pub = await db.BiblePublications
                .Include(p => p.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync();

            Assert.Equal("Second Name", pub.Name);
            var track = Assert.Single(pub.Tracks);
            Assert.Equal("9", track.TrackCode);
            Assert.Contains("update-second.mp3", track.TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }
}
