#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
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
        Assert.Contains("Study", section.Name, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrEmpty(localizedName));

        var track = Assert.Single(section.Tracks);
        Assert.Equal("1", track.TrackCode);
        Assert.Contains("cdn.example", track.TrackUrl?.Url ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
