#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class SaveSectionTracksPersistenceRequestTests
{
    [Fact]
    public async Task SaveSectionTracksPersistenceRequest_Holds_State_And_Deconstructs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var publication = new BiblePublication
        {
            Name = "Persistence section pub",
            PublicationCode = "SSTP-PUB-ID1",
            LanguageId = null,
            Language = null,
            IsVideo = false,
            IsMusic = false,
        };

        var section = new BiblePublicationSection
        {
            Name = "Acts",
            SectionCode = "acts-1",
            BiblePublication = publication,
        };

        publication.Sections.Add(section);

        using var ctSource = new CancellationTokenSource();

        var sut = new SaveSectionTracksPersistenceRequest(
            Db: db,
            Section: section,
            SectionCode: "acts-1",
            PublicationCode: "SSTP-PUB-ID1",
            LanguageCode: "E",
            CancellationToken: ctSource.Token);

        Assert.Same(db, sut.Db);
        Assert.Same(section, sut.Section);
        Assert.Equal("acts-1", sut.SectionCode);
        Assert.Equal("SSTP-PUB-ID1", sut.PublicationCode);
        Assert.Equal("E", sut.LanguageCode);
        Assert.Equal(ctSource.Token, sut.CancellationToken);

        Assert.Equal(sut, sut with { });

        sut.Deconstruct(out var dbOut, out var secOut, out var secCode, out var pubCode, out var langCode, out var ct);
        Assert.Same(db, dbOut);
        Assert.Same(section, secOut);
        Assert.Equal("acts-1", secCode);
        Assert.Equal("SSTP-PUB-ID1", pubCode);
        Assert.Equal("E", langCode);
        Assert.Equal(ctSource.Token, ct);
    }
}
