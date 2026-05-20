#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class LanguageContentServiceBibleAlarmTests
{
    private static string CategoryNameJson(string name)
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        return "{\"" + cat + "\":{\"" + nm + "\":\"" + name + "\"}}";
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
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
    public async Task GetVideoPublicationDisplayNameAsync_Passes_Uppercase_Language_On_Mediator_Path()
    {
        var expectedSegment =
            $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/"
            + $"{AppConstants.Media.DefaultLanguageCode}/"
            + AppConstants.Media.BiblePublicationCodeDramasGoodNews;

        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var handler = new StubHandler(request =>
            {
                var uri = request.RequestUri?.AbsoluteUri ?? "";
                Assert.True(
                    uri.Contains(expectedSegment, StringComparison.OrdinalIgnoreCase),
                    $"Expected path segment {expectedSegment} in {uri}");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(CategoryNameJson("T")),
                };
            });

            using var http = new HttpClient(handler);
            var sut = new LanguageContentService(
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger(),
                http);

            var title = await sut.GetVideoPublicationDisplayNameAsync(
                AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
                languageCode: AppConstants.Media.DefaultLanguageCode.ToLowerInvariant());

            Assert.Equal("T", title);
        }
    }

    [Fact]
    public async Task GetVideoPublicationDisplayNameAsync_Returns_Localized_Name_From_Mediator()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CategoryNameJson("Title&nbsp;A")),
            });
            using var http = new HttpClient(handler);
            var sut = new LanguageContentService(
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger(),
                http);

            var name = await sut.GetVideoPublicationDisplayNameAsync(
                AppConstants.Media.NormalizedPublicationCodeDramasGoodNews.ToUpperInvariant(),
                AppConstants.Media.DefaultLanguageCode);

            Assert.Equal("Title A", name);
        }
    }

    [Fact]
    public async Task FetchPublicationTracksAsync_ReturnsFalse_When_No_Catalog_Data()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new StubHandler(_ =>
                throw new InvalidOperationException("HTTP should not be used.")));
            var sut = new LanguageContentService(
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger(),
                http);

            Assert.False(await sut.FetchPublicationTracksAsync(
                publicationCode: "no-such-flat-pub-wave",
                languageCode: "XX"));
        }
    }

    [Fact]
    public async Task FetchPublicationSectionsAsync_ReturnsFalse_When_No_Catalog_Data()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new StubHandler(_ =>
                throw new InvalidOperationException("HTTP should not be used.")));
            var sut = new LanguageContentService(
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger(),
                http);

            Assert.False(await sut.FetchPublicationSectionsAsync(
                publicationCode: AppConstants.Media.BiblePublicationCodeNwt,
                languageCode: "yy"));
        }
    }

    [Fact]
    public async Task FetchSectionTracksAsync_ReturnsFalse_When_No_Catalog_Data()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new StubHandler(_ =>
                throw new InvalidOperationException("HTTP should not be used.")));
            var sut = new LanguageContentService(
                new MediaTestScopeFactory(options),
                TestLogging.CreateLogger(),
                http);

            Assert.False(await sut.FetchSectionTracksAsync(
                publicationCode: AppConstants.Media.BiblePublicationCodeNwt,
                sectionCode: "mat",
                languageCode: "zz"));
        }
    }
}
