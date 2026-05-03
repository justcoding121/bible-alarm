#nullable enable

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

public sealed class MediatorPublicationBuilderTests
{
    private static BiblePublicationTrack MakeTrack(string code, string url)
    {
        var t = new BiblePublicationTrack { TrackCode = code, Title = "Title-" + code };
        t.TrackUrl = new TrackUrl { Url = url, BiblePublicationTrack = t };
        return t;
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new MediatorPublicationBuilder(null!));
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_ReturnsFalse_When_Tracks_Empty()
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
        var lang = new Language
        {
            LanguageCode = "MED-X-EMPTY",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new MediatorPublicationBuilder(TestLogging.CreateLogger());
        var req = new BuildMediatorPublicationRequest(
            db,
            AppConstants.Media.BiblePublicationCodeDramasGoodNews,
            PublicationName: "Drama empty",
            Language: lang,
            Tracks: [],
            CancellationToken: CancellationToken.None);

        Assert.False(await sut.BuildAndSavePublicationAsync(req));
        Assert.Equal(0, await db.BiblePublications.CountAsync());
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_Inserts_Then_Updates_Tracks_On_Duplicate_Publication()
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

        var categoryCodes = JwSourceHelper.GetCategoryCodesForPublication(AppConstants.Media.BiblePublicationCodeDramasGoodNews);
        foreach (var code in categoryCodes)
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == code))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = code });
        }

        var lang = new Language
        {
            LanguageCode = "MED-Y-PAIR",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var pubCode = AppConstants.Media.BiblePublicationCodeDramasGoodNews;
        var sut = new MediatorPublicationBuilder(TestLogging.CreateLogger());

        var firstTracks = new List<BiblePublicationTrack> { MakeTrack("1", "https://first.example/a.mp3") };
        var req1 = new BuildMediatorPublicationRequest(
            db,
            pubCode,
            PublicationName: "First name",
            Language: lang,
            Tracks: firstTracks,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(req1));

        var secondTracks = new List<BiblePublicationTrack> { MakeTrack("2", "https://second.example/b.mp3") };
        var req2 = new BuildMediatorPublicationRequest(
            db,
            pubCode,
            PublicationName: "Second name",
            Language: lang,
            Tracks: secondTracks,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(req2));

        var persisted = await db.BiblePublications
            .Include(p => p.Tracks)
            .ThenInclude(t => t.TrackUrl)
            .SingleAsync(p => p.PublicationCode == pubCode && p.LanguageId == lang.Id);

        Assert.Equal("Second name", persisted.Name);
        var track = Assert.Single(persisted.Tracks);
        Assert.Equal("2", track.TrackCode);
        Assert.Contains("second.example", track.TrackUrl!.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPublicationCodeForDb_Returns_JwCanonical_When_Mediator_Code_Known()
    {
        var sut = new MediatorPublicationBuilder(TestLogging.CreateLogger());
        var input = AppConstants.Media.NormalizedPublicationCodeDramasGoodNews;
        var expected = JwSourceHelper.GetCanonicalMediatorPublicationCode(input);
        Assert.NotNull(expected);
        Assert.Equal(expected, sut.GetPublicationCodeForDb(input));
    }

    [Fact]
    public void GetPublicationCodeForDb_Returns_Input_When_Mediator_Code_Unknown()
    {
        var sut = new MediatorPublicationBuilder(TestLogging.CreateLogger());
        const string code = "no-such-mediator-w4";
        Assert.Equal(code, sut.GetPublicationCodeForDb(code));
    }

    [Fact]
    public void GetPublicationCodeForDb_Returns_Null_Or_Empty_For_Null_Or_Blank()
    {
        var sut = new MediatorPublicationBuilder(TestLogging.CreateLogger());
        Assert.Null(sut.GetPublicationCodeForDb(null!));
        Assert.Equal("", sut.GetPublicationCodeForDb(""));
    }
}
