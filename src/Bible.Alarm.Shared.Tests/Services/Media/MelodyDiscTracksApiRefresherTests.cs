#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class MelodyDiscTracksApiRefresherTests
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
    public async Task ReplaceDiscSectionTracks_ReturnsFalse_When_NoLanguage_Publication_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            var sut = new MelodyDiscTracksApiRefresher(
                new MediaTestScopeFactory(options),
                new HttpClient(new JsonHandler("{}")),
                TestLogging.CreateLogger());

            Assert.False(await sut.ReplaceDiscSectionTracksFromApiAsync("iam", "iam-1"));
        }
    }

    [Fact]
    public async Task ReplaceDiscSectionTracks_ReturnsFalse_When_Section_Missing()
    {
        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        await using (var db = new MediaDbContext(options))
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            db.Categories.Add(musicCat);
            await db.SaveChangesAsync();

            db.BiblePublications.Add(new BiblePublication
            {
                Name = "Melody",
                PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                LanguageId = null,
                Language = null,
                IsVideo = false,
                IsMusic = true,
                BiblePublicationCategories =
                [
                    new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                ],
                Sections = [],
                Tracks = [],
            });
            await db.SaveChangesAsync();

            var sut = new MelodyDiscTracksApiRefresher(
                new MediaTestScopeFactory(options),
                new HttpClient(new JsonHandler("{}")),
                TestLogging.CreateLogger());

            Assert.False(await sut.ReplaceDiscSectionTracksFromApiAsync(
                AppConstants.Media.MelodyMusicPublicationCodeIam,
                "iam-9"));
        }
    }

    [Fact]
    public async Task ReplaceDiscSectionTracks_ReturnsTrue_When_Fetch_Succeeds()
    {
        const string json =
            "{\"files\":{\"E\":{\"MP3\":[{\"file\":{\"url\":\"https://melody.example/t.mp3\"},\"track\":7,\"title\":\"Track\"}]}},\"pubName\":\"Melody\"}";

        var (connection, options) = await CreateConnectionAndOptionsAsync();
        await using (connection)
        {
            await using (var db = new MediaDbContext(options))
            {
                var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
                db.Categories.Add(musicCat);
                await db.SaveChangesAsync();

                var pub = new BiblePublication
                {
                    Name = "Melody",
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    LanguageId = null,
                    Language = null,
                    IsVideo = false,
                    IsMusic = true,
                    BiblePublicationCategories =
                    [
                        new BiblePublicationCategory { Category = musicCat, CategoryId = musicCat.Id },
                    ],
                    Sections =
                    [
                        new BiblePublicationSection
                        {
                            Name = "Disc",
                            SectionCode = "iam-9",
                            Tracks = [],
                        },
                    ],
                    Tracks = [],
                };
                db.BiblePublications.Add(pub);
                await db.SaveChangesAsync();

                using var http = new HttpClient(new JsonHandler(json));
                var sut = new MelodyDiscTracksApiRefresher(
                    new MediaTestScopeFactory(options),
                    http,
                    TestLogging.CreateLogger());

                Assert.True(await sut.ReplaceDiscSectionTracksFromApiAsync(
                    AppConstants.Media.MelodyMusicPublicationCodeIam,
                    "Iam-9"));
            }

            await using (var verify = new MediaDbContext(options))
            {
                var section = await verify.BiblePublicationSections
                    .Include(s => s.Tracks)
                    .ThenInclude(t => t.TrackUrl)
                    .SingleAsync(s => s.SectionCode == "iam-9");

                var track = Assert.Single(section.Tracks);
                Assert.Equal("7", track.TrackCode);
                Assert.Contains("melody.example", track.TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
