#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FetchEnglishSectionsRequestTests
{
    [Fact]
    public async Task FetchEnglishSectionsRequest_Holds_Structural_State_And_Deconstructs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        List<string> sectionCodes = ["gen", "rev"];
        using var ctSource = new CancellationTokenSource();

        var sut = new FetchEnglishSectionsRequest(
            Db: db,
            NormalizedPublicationCode: "NWTST",
            NormalizedLanguageCode: "ENG",
            CategoryName: "BibleTeaching",
            SectionCodes: sectionCodes,
            IsVideo: true,
            CancellationToken: ctSource.Token);

        Assert.Same(db, sut.Db);
        Assert.Equal("NWTST", sut.NormalizedPublicationCode);
        Assert.Equal("ENG", sut.NormalizedLanguageCode);
        Assert.Equal("BibleTeaching", sut.CategoryName);
        Assert.Same(sectionCodes, sut.SectionCodes);
        Assert.True(sut.IsVideo);
        Assert.Equal(ctSource.Token, sut.CancellationToken);

        sut.Deconstruct(out var dbOut, out var npc, out var nlc, out var category, out var codes, out var isVideo, out var ct);
        Assert.Same(db, dbOut);
        Assert.Equal("NWTST", npc);
        Assert.Equal("ENG", nlc);
        Assert.Equal("BibleTeaching", category);
        Assert.Same(sectionCodes, codes);
        Assert.True(isVideo);
        Assert.Equal(ctSource.Token, ct);
    }
}
