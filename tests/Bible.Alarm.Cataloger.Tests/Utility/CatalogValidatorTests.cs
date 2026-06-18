#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class CatalogValidatorTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    private static MediaDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MediaDbContext(opts);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static async Task SeedNwtEnglishTrackAsync(MediaDbContext db, string trackUrl)
    {
        var english = new Language { LanguageCode = AppConstants.Media.DefaultLanguageCode, Direction = "ltr" };
        db.Languages.Add(english);
        await db.SaveChangesAsync();

        var publication = new BiblePublication
        {
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            Name = "New World Translation",
            LanguageId = english.Id,
            Language = english,
            IsVideo = false,
            IsMusic = false,
            CatalogType = CatalogType.Sectioned,
        };
        db.BiblePublications.Add(publication);
        await db.SaveChangesAsync();

        var section = new BiblePublicationSection
        {
            SectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber,
            Name = "Genesis",
            BiblePublicationId = publication.Id,
            BiblePublication = publication,
        };
        db.BiblePublicationSections.Add(section);
        await db.SaveChangesAsync();

        var track = new BiblePublicationTrack
        {
            TrackCode = AppConstants.Media.BiblePublicationGenesisBookNumber,
            Title = "Genesis 1",
            BiblePublicationId = publication.Id,
            Publication = publication,
            BiblePublicationSectionId = section.Id,
            Section = section,
        };
        db.BiblePublicationTracks.Add(track);
        await db.SaveChangesAsync();

        db.TrackUrls.Add(new TrackUrl
        {
            Url = trackUrl,
            BiblePublicationTrackId = track.Id,
            BiblePublicationTrack = track,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ValidateAsync_completes_when_head_request_succeeds()
    {
        await using var db = CreateDb();
        const string trackUrl = "https://cdn.example.com/nwt-1.mp3";
        await SeedNwtEnglishTrackAsync(db, trackUrl);

        using var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);

        await CatalogValidator.ValidateAsync(db, httpClient, SilentLogger);

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Head, handler.Requests[0].Method);
        Assert.Equal(trackUrl, handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task ValidateAsync_falls_back_to_get_when_head_not_allowed()
    {
        await using var db = CreateDb();
        const string trackUrl = "https://cdn.example.com/nwt-1.mp3";
        await SeedNwtEnglishTrackAsync(db, trackUrl);

        var callCount = 0;
        using var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler);

        await CatalogValidator.ValidateAsync(db, httpClient, SilentLogger);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Head, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
    }

    [Fact]
    public async Task ValidateAsync_does_not_throw_when_db_has_no_sample_tracks()
    {
        await using var db = CreateDb();
        using var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);

        await CatalogValidator.ValidateAsync(db, httpClient, SilentLogger);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ValidateEnglishSeedContentAsync_returns_true_when_seeded_publication_has_tracks()
    {
        await using var db = CreateDb();
        await SeedNwtEnglishTrackAsync(db, "https://cdn.example.com/nwt-1.mp3");

        var result = await CatalogValidator.ValidateEnglishSeedContentAsync(
            db,
            SilentLogger,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppConstants.Media.BiblePublicationCodeNwt });

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateEnglishSeedContentAsync_returns_false_when_publication_missing()
    {
        await using var db = CreateDb();

        var result = await CatalogValidator.ValidateEnglishSeedContentAsync(
            db,
            SilentLogger,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppConstants.Media.BiblePublicationCodeNwt });

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateEnglishSeedContentAsync_returns_false_when_sectioned_publication_has_no_sections()
    {
        await using var db = CreateDb();
        var english = new Language { LanguageCode = AppConstants.Media.DefaultLanguageCode, Direction = "ltr" };
        db.Languages.Add(english);
        await db.SaveChangesAsync();

        db.BiblePublications.Add(new BiblePublication
        {
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            Name = "New World Translation",
            LanguageId = english.Id,
            Language = english,
            IsVideo = false,
            IsMusic = false,
            CatalogType = CatalogType.Sectioned,
            Tracks =
            [
                new BiblePublicationTrack
                {
                    TrackCode = "1",
                    Title = "Genesis 1",
                },
            ],
        });
        await db.SaveChangesAsync();

        var result = await CatalogValidator.ValidateEnglishSeedContentAsync(
            db,
            SilentLogger,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppConstants.Media.BiblePublicationCodeNwt });

        Assert.False(result);
    }
}
