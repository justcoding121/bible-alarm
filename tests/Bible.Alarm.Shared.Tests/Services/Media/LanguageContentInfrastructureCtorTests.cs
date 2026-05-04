#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageContentInfrastructureCtorTests
{
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

    [Fact]
    public async Task LanguageContentService_Throws_When_ScopeFactoryNull()
    {
        var (connection, _) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new JsonHandler("{}"));
            var logger = TestLogging.CreateLogger();

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentService(null!, logger, http));
        }
    }

    [Fact]
    public async Task LanguageContentService_Throws_When_LoggerNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            using var http = new HttpClient(new JsonHandler("{}"));
            var factory = new MediaTestScopeFactory(options);

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentService(factory, null!, http));
        }
    }

    [Fact]
    public async Task LanguageContentService_Throws_When_HttpClientNull()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            var factory = new MediaTestScopeFactory(options);
            var logger = TestLogging.CreateLogger();

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentService(factory, logger, null!));
        }
    }

    [Fact]
    public async Task LanguageContentPublicationTracksFetcher_Throws_On_Null_Args()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            var factory = new MediaTestScopeFactory(options);
            var logger = TestLogging.CreateLogger();
            using var http = new HttpClient(new JsonHandler("{}"));
            var mediator = new MediatorFetcher(http, logger);
            var video = new VideoLocalizedNameFetcher(http, logger);
            var flat = new FlatPublicationFetcher(http, logger, video);

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentPublicationTracksFetcher(null!, logger, mediator, flat));
            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentPublicationTracksFetcher(factory, null!, mediator, flat));
            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentPublicationTracksFetcher(factory, logger, null!, flat));
            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentPublicationTracksFetcher(factory, logger, mediator, null!));
        }
    }

    [Fact]
    public async Task LanguageContentPublicationSectionsFetcher_Throws_On_Null_Args()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            var factory = new MediaTestScopeFactory(options);
            var logger = TestLogging.CreateLogger();
            using var http = new HttpClient(new JsonHandler("{}"));
            var sectionFetcher = new SectionFetcher(http, logger);

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentPublicationSectionsFetcher(null!, logger, sectionFetcher));
            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentPublicationSectionsFetcher(factory, null!, sectionFetcher));
            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentPublicationSectionsFetcher(factory, logger, null!));
        }
    }

    [Fact]
    public async Task LanguageContentSectionTracksFetcher_Throws_On_Null_Args()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            var factory = new MediaTestScopeFactory(options);
            var logger = TestLogging.CreateLogger();
            using var http = new HttpClient(new JsonHandler("{}"));
            var sectionFetcher = new SectionFetcher(http, logger);

            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentSectionTracksFetcher(null!, logger, sectionFetcher));
            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentSectionTracksFetcher(factory, null!, sectionFetcher));
            Assert.Throws<ArgumentNullException>(() =>
                new LanguageContentSectionTracksFetcher(factory, logger, null!));
        }
    }
}
