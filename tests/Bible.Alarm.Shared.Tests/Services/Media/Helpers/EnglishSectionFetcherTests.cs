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

public sealed class EnglishSectionFetcherTests
{
    private const string MinimalBibleSectionJson =
        "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/t.mp3\"},\"track\":1,\"title\":\"Matt\"}]}},\"pubName\":\"Study&nbsp;Edition\",\"parentPubName\":\"Worldwide&nbsp;Study\"}";

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

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                RequestUris.Add(request.RequestUri);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class FirstThrowThenJsonHandler(Exception first, string body) : HttpMessageHandler
    {
        private int calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                return Task.FromException<HttpResponseMessage>(first);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private static async Task<(SqliteConnection Connection, MediaDbContext Db)> OpenDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        return (connection, new MediaDbContext(options));
    }

    [Fact]
    public void Constructor_Throws_When_HttpClientNull()
    {
        Assert.Throws<ArgumentNullException>(() => new EnglishSectionFetcher(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_Throws_When_LoggerNull()
    {
        using var http = new HttpClient(new JsonHandler("{}"));
        Assert.Throws<ArgumentNullException>(() => new EnglishSectionFetcher(http, null!));
    }

    [Fact]
    public async Task FetchSectionsAsync_ParseIamTracks_WhenPublicationLanguage_Has_NullLanguageId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);
        var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        db.Categories.Add(musicCat);
        await db.SaveChangesAsync();

        db.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            LanguageId = null,
            Language = null,
            CategoryId = musicCat.Id,
            Category = musicCat,
            IsMusic = true,
        });
        await db.SaveChangesAsync();

        const string iamJson =
            "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn/iam-song\"},\"track\":1,\"title\":\"Tune\"}]}},\"pubName\":\"KM&nbsp;Deck\"}";
        using var http = new HttpClient(new JsonHandler(iamJson));
        var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

        var req = new FetchEnglishSectionsRequest(
            Db: db,
            NormalizedPublicationCode: AppConstants.Media.MelodyMusicPublicationCodeIam,
            NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
            CategoryName: AppConstants.Media.BiblePublicationCategoryMusic,
            SectionCodes: ["101"],
            IsVideo: false,
            CancellationToken: CancellationToken.None);

        var (sections, localizedName) = await sut.FetchSectionsAsync(req);

        var section = Assert.Single(sections);
        Assert.Equal("101", section.SectionCode, StringComparer.OrdinalIgnoreCase);
        Assert.True(section.Name.Contains("KM Deck", StringComparison.OrdinalIgnoreCase));
        var track = Assert.Single(section.Tracks);
        Assert.Equal("https://cdn/iam-song", track.TrackUrl?.Url, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Tune", track.Title);

        // ExtractPublicationNameAsync only uses parentPubName / category.name for non-Bible; IAM JSON has pubName only.
        Assert.Null(localizedName);
    }

    [Fact]
    public async Task FetchSectionsAsync_Reads_LocalizedName_From_Category_Node_For_NonBibleVideo_Section()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var dramaJson =
            "{\"files\":{\"E\":{\"MP4\":[{\"file\":{\"url\":\"https://v/ep.mp4\"},\"track\":1,\"title\":\"Pilot\"}]}},\"category\":{\"name\":\"Vid&nbsp;Group\"}}";
        using var http = new HttpClient(new JsonHandler(dramaJson));
        var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

        var req = new FetchEnglishSectionsRequest(
            Db: db,
            NormalizedPublicationCode: AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
            NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
            CategoryName: AppConstants.Media.BiblePublicationCategoryDramas,
            SectionCodes: ["m-test"],
            IsVideo: true,
            CancellationToken: CancellationToken.None);

        var (sections, localizedName) = await sut.FetchSectionsAsync(req);

        Assert.NotNull(localizedName);
        Assert.Equal("Vid Group", localizedName);

        var section = Assert.Single(sections);
        Assert.Equal("m-test", section.SectionCode, StringComparer.OrdinalIgnoreCase);
        var track = Assert.Single(section.Tracks);
        Assert.True(track.TrackUrl!.Url.Contains("v/ep.mp4", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FetchSectionsAsync_ReturnsEmpty_When_No_Section_Json_From_Api()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);
        using var http = new HttpClient(new NotFoundHandler());
        var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

        var req = new FetchEnglishSectionsRequest(
            Db: db,
            NormalizedPublicationCode: AppConstants.Media.BiblePublicationCodeNwt,
            NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
            CategoryName: AppConstants.Media.BiblePublicationCategoryBible,
            SectionCodes: ["mat"],
            IsVideo: false,
            CancellationToken: CancellationToken.None);

        var (sections, name) = await sut.FetchSectionsAsync(req);

        Assert.Empty(sections);
        Assert.Null(name);
    }

    [Fact]
    public async Task FetchSectionsAsync_BuildsSection_And_Decodes_Name_For_Wt_Bible_Section()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);
        using var http = new HttpClient(new JsonHandler(MinimalBibleSectionJson));
        var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

        var req = new FetchEnglishSectionsRequest(
            Db: db,
            NormalizedPublicationCode: AppConstants.Media.BiblePublicationCodeNwt,
            NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
            CategoryName: AppConstants.Media.BiblePublicationCategoryBible,
            SectionCodes: ["Mat"],
            IsVideo: false,
            CancellationToken: CancellationToken.None);

        var (sections, localizedName) = await sut.FetchSectionsAsync(req);

        var section = Assert.Single(sections);
        Assert.Equal("mat", section.SectionCode, StringComparer.OrdinalIgnoreCase);
        Assert.True(section.Name.Contains("Study", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(localizedName));

        var track = Assert.Single(section.Tracks);
        Assert.Equal("1", track.TrackCode);
        Assert.True((track.TrackUrl?.Url ?? "").Contains("cdn.example", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FetchSectionsAsync_Database_Overload_Infers_IsVideo_From_PublicationCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var bootstrap = new MediaDbContext(options))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var db = new MediaDbContext(options);

        var dramaJson =
            "{\"files\":{\"E\":{\"MP4\":[{\"file\":{\"url\":\"https://v/ep.mp4\"},\"track\":1,\"title\":\"Pilot\"}]}},\"category\":{\"name\":\"Vid&nbsp;Group\"}}";
        using var http = new HttpClient(new JsonHandler(dramaJson));
        var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

        var (sections, localizedName) = await sut.FetchSectionsAsync(
            db,
            normalizedPublicationCode: AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
            normalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
            categoryName: AppConstants.Media.BiblePublicationCategoryDramas,
            sectionCodes: ["m-test"],
            cancellationToken: CancellationToken.None);

        Assert.NotNull(localizedName);
        Assert.Equal("Vid Group", localizedName);

        var section = Assert.Single(sections);
        Assert.Equal("m-test", section.SectionCode, StringComparer.OrdinalIgnoreCase);
        Assert.Single(section.Tracks);
    }

    [Fact]
    public async Task FetchSectionsAsync_Bible_Query_Uses_Publication_Code_And_Booknum()
    {
        var (connection, db) = await OpenDbAsync();
        await using (connection)
        await using (db)
        {
            var handler = new RecordingHandler(MinimalBibleSectionJson);
            using var http = new HttpClient(handler);
            var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

            var (sections, _) = await sut.FetchSectionsAsync(new FetchEnglishSectionsRequest(
                Db: db,
                NormalizedPublicationCode: AppConstants.Media.BiblePublicationCodeNwt,
                NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
                CategoryName: AppConstants.Media.BiblePublicationCategoryBible,
                SectionCodes: ["Mat"],
                IsVideo: false,
                CancellationToken: CancellationToken.None));

            Assert.Single(sections);
            Assert.NotEmpty(handler.RequestUris);
            foreach (var uri in handler.RequestUris)
            {
                var query = uri.Query;
                Assert.Contains("pub=nwt", query, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("booknum=Mat", query, StringComparison.Ordinal);
                Assert.Contains("fileformat=MP3", query, StringComparison.OrdinalIgnoreCase);
                Assert.Contains($"langwritten={AppConstants.Media.DefaultLanguageCode}", query, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("pub=Mat", query, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task FetchSectionsAsync_Magazine_Query_Uses_Issue_And_Year_As_Localized_Name()
    {
        var (connection, db) = await OpenDbAsync();
        await using (connection)
        await using (db)
        {
            const string magazineJson =
                "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/wt.mp3\"},\"track\":1,\"title\":\"Study\"}]}},\"pubName\":\"Watchtower\",\"formattedDate\":\"Feb\"}";
            var handler = new RecordingHandler(magazineJson);
            using var http = new HttpClient(handler);
            var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

            var (sections, localizedName) = await sut.FetchSectionsAsync(new FetchEnglishSectionsRequest(
                Db: db,
                NormalizedPublicationCode: "w2020",
                NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
                CategoryName: AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine,
                SectionCodes: ["20090201-wp"],
                IsVideo: false,
                CancellationToken: CancellationToken.None));

            var section = Assert.Single(sections);
            Assert.Equal("2020", localizedName);
            Assert.Contains("Feb", section.Name, StringComparison.OrdinalIgnoreCase);

            var query = Assert.Single(handler.RequestUris).Query;
            Assert.Contains("pub=wp", query, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("issue=20090201", query, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("booknum=", query, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchSectionsAsync_NoLanguage_Publication_Uses_Section_As_Pub_And_English_Langwritten()
    {
        var (connection, db) = await OpenDbAsync();
        await using (connection)
        await using (db)
        {
            db.BiblePublications.Add(new BiblePublication
            {
                PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                Name = "IAM",
                LanguageId = null,
                Language = null,
                IsMusic = true,
                IsVideo = false,
                Sections = [],
                Tracks = [],
            });
            await db.SaveChangesAsync();

            const string iamJson =
                "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn/iam-song\"},\"track\":1,\"title\":\"Tune\"}]}},\"pubName\":\"KM&nbsp;Deck\"}";
            var handler = new RecordingHandler(iamJson);
            using var http = new HttpClient(handler);
            var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

            var (sections, _) = await sut.FetchSectionsAsync(new FetchEnglishSectionsRequest(
                Db: db,
                NormalizedPublicationCode: AppConstants.Media.MelodyMusicPublicationCodeIam,
                NormalizedLanguageCode: "F",
                CategoryName: AppConstants.Media.BiblePublicationCategoryMusic,
                SectionCodes: ["101"],
                IsVideo: false,
                CancellationToken: CancellationToken.None));

            Assert.Single(sections);
            Assert.NotEmpty(handler.RequestUris);
            var sectionQuery = handler.RequestUris[0].Query;
            Assert.Contains("pub=101", sectionQuery, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"langwritten={AppConstants.Media.DefaultLanguageCode}", sectionQuery, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("booknum=", sectionQuery, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FetchSectionsAsync_Ignores_Misleading_Bible_ParentPubName()
    {
        var (connection, db) = await OpenDbAsync();
        await using (connection)
        await using (db)
        {
            var misleadingJson =
                "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://cdn.example/t.mp3\"},\"track\":1,\"title\":\"Matt\"}]}},\"pubName\":\"NWT\",\"parentPubName\":\"The Good News According to Jesus\"}";
            using var http = new HttpClient(new JsonHandler(misleadingJson));
            var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

            var (sections, localizedName) = await sut.FetchSectionsAsync(new FetchEnglishSectionsRequest(
                Db: db,
                NormalizedPublicationCode: AppConstants.Media.BiblePublicationCodeNwt,
                NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
                CategoryName: AppConstants.Media.BiblePublicationCategoryBible,
                SectionCodes: ["mat"],
                IsVideo: false,
                CancellationToken: CancellationToken.None));

            Assert.Single(sections);
            Assert.Null(localizedName);
        }
    }

    [Fact]
    public async Task FetchSectionsAsync_Continues_After_One_Section_Fails()
    {
        var (connection, db) = await OpenDbAsync();
        await using (connection)
        await using (db)
        {
            using var http = new HttpClient(new FirstThrowThenJsonHandler(
                new InvalidOperationException("simulated section failure"),
                MinimalBibleSectionJson));
            var sut = new EnglishSectionFetcher(http, TestLogging.CreateLogger());

            var (sections, localizedName) = await sut.FetchSectionsAsync(new FetchEnglishSectionsRequest(
                Db: db,
                NormalizedPublicationCode: AppConstants.Media.BiblePublicationCodeNwt,
                NormalizedLanguageCode: AppConstants.Media.DefaultLanguageCode,
                CategoryName: AppConstants.Media.BiblePublicationCategoryBible,
                SectionCodes: ["mat", "mrk"],
                IsVideo: false,
                CancellationToken: CancellationToken.None));

            var section = Assert.Single(sections);
            Assert.Equal("mrk", section.SectionCode, StringComparer.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrEmpty(localizedName));
        }
    }
}
