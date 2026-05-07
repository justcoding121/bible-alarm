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

    private sealed class BrokenJsonOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{not-valid-json-for-getpub"),
            });
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

    private static async Task SeedDramasGoodNewsCategoriesAndFrenchLanguageAsync(MediaDbContext db)
    {
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(
                     AppConstants.Media.NormalizedPublicationCodeDramasGoodNews))
        {
            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        db.Languages.Add(new Language
        {
            LanguageCode = "F",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        });

        await db.SaveChangesAsync();
    }

    private static string MediatorCategoryNameJson(string name)
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        return "{\"" + cat + "\":{\"" + nm + "\":\"" + name + "\"}}";
    }

    private sealed class RouteMediatorThenGetPubFactoryHandler : HttpMessageHandler
    {
        private readonly string mediatorBody;
        private readonly string getPubBody;

        internal RouteMediatorThenGetPubFactoryHandler(string mediatorBody, string getPubBody)
        {
            this.mediatorBody = mediatorBody;
            this.getPubBody = getPubBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? "";
            var body = uri.Contains(AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix, StringComparison.OrdinalIgnoreCase)
                ? mediatorBody
                : getPubBody;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
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

    [Fact]
    public async Task Fetch_ReturnsFalse_When_No_Categories_Seeded_For_Publication()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"track\":1,\"file\":{\"url\":\"https://cdn.example/osg.mp3\"},\"title\":\"Song One\"}]}},\"pubName\":\"Sing&nbsp;Out\"}";

        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            db.Languages.Add(new Language
            {
                LanguageCode = AppConstants.Media.DefaultLanguageCode,
                Direction = AppConstants.Media.TextDirectionLeftToRight
            });
            await db.SaveChangesAsync();
            var lang = await db.Languages.SingleAsync();

            using var handler = new JsonHandler(json);
            using var httpClient = new HttpClient(handler);
            var sut = new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = new BiblePublication
                {
                    Name = "Songs EN",
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

            Assert.False(await sut.FetchFlatPublicationTracksAsync(req));
            Assert.Equal(0, await db.BiblePublications.CountAsync());
        }
    }

    [Fact]
    public async Task Fetch_Resolves_Language_From_Database_When_Request_Language_Null()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"track\":1,\"file\":{\"url\":\"https://cdn.example/osg.mp3\"},\"title\":\"Song One\"}]}},\"pubName\":\"Sing&nbsp;Out\"}";

        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            await SeedOsgCategoriesAndLanguageAsync(db);

            using var handler = new JsonHandler(json);
            using var httpClient = new HttpClient(handler);
            var sut = new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = "osg",
                    IsVideo = false,
                    IsMusic = true,
                    Sections = [],
                    Tracks = []
                },
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                Language = null,
                CancellationToken = CancellationToken.None
            };

            Assert.True(await sut.FetchFlatPublicationTracksAsync(req));
            var pub = await db.BiblePublications.SingleAsync();
            Assert.NotNull(pub.LanguageId);
        }
    }

    [Fact]
    public async Task Fetch_ReturnsFalse_When_Target_Language_Missing_And_Request_Language_Null()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"track\":1,\"file\":{\"url\":\"https://cdn.example/osg.mp3\"},\"title\":\"Song One\"}]}},\"pubName\":\"Sing&nbsp;Out\"}";

        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication("osg"))
            {
                db.Categories.Add(new Category { CategoryCode = categoryCode });
            }

            await db.SaveChangesAsync();

            using var handler = new JsonHandler(json);
            using var httpClient = new HttpClient(handler);
            var sut = new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = "osg",
                    IsVideo = false,
                    IsMusic = true,
                    Sections = [],
                    Tracks = []
                },
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                Language = null,
                CancellationToken = CancellationToken.None
            };

            Assert.False(await sut.FetchFlatPublicationTracksAsync(req));
        }
    }

    [Fact]
    public async Task Fetch_Applies_LocalizedMediatorTitle_When_Video_And_Language_Is_Not_English()
    {
        // Omit pubName so TryDecodePublicationNameFromRoot does not overwrite the Mediator title (see FlatPublicationFetcher).
        const string frenchMp4FlatJson =
            "{\"files\":{\"F\":{\"MP4\":[{\"track\":1,\"file\":{\"url\":\"https://cdn/fr-scene.mp4\"},\"title\":\"Scène\"}]}}}";

        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            await SeedDramasGoodNewsCategoriesAndFrenchLanguageAsync(db);
            var lang = await db.Languages.SingleAsync(l => l.LanguageCode == "F");

            var mediatorBody = MediatorCategoryNameJson("Titre dramas FR");

            using var routed = new RouteMediatorThenGetPubFactoryHandler(mediatorBody, frenchMp4FlatJson);
            using var httpClient = new HttpClient(routed);
            var sut = new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

            var normalized = AppConstants.Media.NormalizedPublicationCodeDramasGoodNews;
            var englishStub = new BiblePublication
            {
                Name = "Good News EN",
                PublicationCode = normalized,
                IsVideo = true,
                IsMusic = false,
                Sections = [],
                Tracks = [],
            };

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = normalized,
                NormalizedLanguageCode = "F",
                EnglishPublication = englishStub,
                IsVideo = true,
                IsMusic = false,
                FileFormat = AppConstants.Media.MediaStreamFormatMp4,
                Language = lang,
                CancellationToken = CancellationToken.None,
            };

            Assert.True(await sut.FetchFlatPublicationTracksAsync(req));

            var pub = await db.BiblePublications
                .Include(p => p.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync();
            Assert.Equal("Titre dramas FR", pub.Name);
            Assert.Single(pub.Tracks);
            Assert.Contains("cdn/fr-scene.mp4", pub.Tracks[0].TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Fetch_ReturnsFalse_When_GetPub_Response_Is_Not_Parseable_As_Json()
    {
        var (connection, db) = await CreateDbAsync();
        await using (connection)
        await using (db)
        {
            await SeedOsgCategoriesAndLanguageAsync(db);
            var lang = await db.Languages.SingleAsync();

            using var httpClient = new HttpClient(new BrokenJsonOkHandler());
            var sut = new FlatPublicationFetcher(
                httpClient,
                TestLogging.CreateLogger(),
                new VideoLocalizedNameFetcher(httpClient, TestLogging.CreateLogger()));

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = "osg",
                    IsVideo = false,
                    IsMusic = true,
                    Sections = [],
                    Tracks = [],
                },
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                Language = lang,
                CancellationToken = CancellationToken.None,
            };

            Assert.False(await sut.FetchFlatPublicationTracksAsync(req));
            Assert.Equal(0, await db.BiblePublications.CountAsync());
        }
    }

    [Fact]
    public async Task Fetch_Skips_AudioDescription_Tracks_And_Keeps_NonMarked_Track_For_Language_E()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP4\":[" +
            "{\"track\":1,\"file\":{\"url\":\"https://cdn/ad-track.mp4\"},\"title\":\"Scene With Audio Descriptions\"}," +
            "{\"track\":2,\"file\":{\"url\":\"https://cdn/main.mp4\"},\"title\":\"Main scene\"}" +
            "]}},\"pubName\":\"Drama\"}";

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

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = "osg",
                    IsVideo = false,
                    IsMusic = true,
                    Sections = [],
                    Tracks = [],
                },
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp4,
                Language = lang,
                CancellationToken = CancellationToken.None,
            };

            Assert.True(await sut.FetchFlatPublicationTracksAsync(req));

            var pub = await db.BiblePublications
                .Include(p => p.Tracks)
                .ThenInclude(t => t.TrackUrl)
                .SingleAsync();

            Assert.Equal("Drama", pub.Name);
            var track = Assert.Single(pub.Tracks);
            Assert.Equal("2", track.TrackCode);
            Assert.Contains("cdn/main.mp4", track.TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Main scene", track.Title);
        }
    }

    [Fact]
    public async Task Fetch_Prefers_240p_Label_When_Duplicate_Track_Number_On_Same_Format()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[" +
            "{\"track\":1,\"label\":\"720p\",\"file\":{\"url\":\"https://cdn/track-hi.mp3\"},\"title\":\"Same Title\"}," +
            "{\"track\":1,\"label\":\"" +
            AppConstants.Media.VideoQualityLabel240p +
            "\",\"file\":{\"url\":\"https://cdn/track-low.mp3\"},\"title\":\"Same Title\"}" +
            "]}},\"pubName\":\"Quality\"}";

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

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = "osg",
                    IsVideo = false,
                    IsMusic = true,
                    Sections = [],
                    Tracks = [],
                },
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                Language = lang,
                CancellationToken = CancellationToken.None,
            };

            Assert.True(await sut.FetchFlatPublicationTracksAsync(req));

            var track = Assert.Single((await db.BiblePublications
                    .Include(p => p.Tracks)
                    .ThenInclude(t => t.TrackUrl)
                    .SingleAsync())
                .Tracks);

            Assert.Equal("1", track.TrackCode);
            Assert.Contains("track-low.mp3", track.TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Fetch_ReturnsFalse_When_Only_Track_Number_Zero_In_Response()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[" +
            "{\"track\":0,\"file\":{\"url\":\"https://cdn/zero.mp3\"},\"title\":\"Ignored\"}" +
            "]}}}";

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

            var req = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "osg",
                NormalizedLanguageCode = AppConstants.Media.DefaultLanguageCode,
                EnglishPublication = new BiblePublication
                {
                    Name = "Songs EN",
                    PublicationCode = "osg",
                    IsVideo = false,
                    IsMusic = true,
                    Sections = [],
                    Tracks = [],
                },
                IsVideo = false,
                IsMusic = true,
                FileFormat = AppConstants.Media.MediaStreamFormatMp3,
                Language = lang,
                CancellationToken = CancellationToken.None,
            };

            Assert.False(await sut.FetchFlatPublicationTracksAsync(req));
            Assert.Equal(0, await db.BiblePublications.CountAsync());
        }
    }
}
