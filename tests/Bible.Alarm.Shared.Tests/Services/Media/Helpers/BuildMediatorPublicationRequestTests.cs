#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BuildMediatorPublicationRequestTests
{
    [Fact]
    public async Task BuildMediatorPublicationRequest_Holds_State_And_Deconstructs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var language = new Language
        {
            LanguageCode = "MED-PUB-L1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        List<BiblePublicationTrack> tracks = [];

        using var cts = new CancellationTokenSource();

        var sut = new BuildMediatorPublicationRequest(
            Db: db,
            PublicationCodeForDb: "MEDIATOR-DB-CODE",
            PublicationName: "Drama display",
            Language: language,
            Tracks: tracks,
            CancellationToken: cts.Token);

        Assert.Same(db, sut.Db);
        Assert.Equal("MEDIATOR-DB-CODE", sut.PublicationCodeForDb);
        Assert.Equal("Drama display", sut.PublicationName);
        Assert.Same(language, sut.Language);
        Assert.Same(tracks, sut.Tracks);
        Assert.Equal(cts.Token, sut.CancellationToken);

        sut.Deconstruct(out var dbOut, out var pubDb, out var name, out var langOut, out var trk, out var ct);
        Assert.Same(db, dbOut);
        Assert.Equal("MEDIATOR-DB-CODE", pubDb);
        Assert.Equal("Drama display", name);
        Assert.Same(language, langOut);
        Assert.Same(tracks, trk);
        Assert.Equal(cts.Token, ct);
    }

    [Fact]
    public async Task BuildMediatorPublicationRequest_Allows_Null_Display_Name()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var language = new Language
        {
            LanguageCode = "MED-PUB-N1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        List<BiblePublicationTrack> tracks = [];

        var sut = new BuildMediatorPublicationRequest(
            Db: db,
            PublicationCodeForDb: "X",
            PublicationName: null,
            Language: language,
            Tracks: tracks,
            CancellationToken: CancellationToken.None);

        Assert.Null(sut.PublicationName);

        sut.Deconstruct(out _, out _, out var name, out _, out _, out _);
        Assert.Null(name);
    }
}
