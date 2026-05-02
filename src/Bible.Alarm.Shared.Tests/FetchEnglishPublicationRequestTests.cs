#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class FetchEnglishPublicationRequestTests
{
    [Fact]
    public async Task FetchEnglishPublicationSectionsRequest_Holds_Structural_State()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var sections = new List<string> { "gen", "exo" };

        Language? absentLanguage = null;
        using var cancellation = new CancellationTokenSource();

        var sut = new FetchEnglishPublicationSectionsRequest(
            Db: db,
            NormalizedPublicationCode: "BI12",
            NormalizedLanguageCode: "ENG",
            Language: absentLanguage,
            CategoryName: "Bible",
            IsVideo: false,
            SectionCodes: sections,
            CancellationToken: cancellation.Token);

        Assert.Same(db, sut.Db);
        Assert.Equal("BI12", sut.NormalizedPublicationCode);
        Assert.Equal("ENG", sut.NormalizedLanguageCode);
        Assert.Null(sut.Language);
        Assert.Equal("Bible", sut.CategoryName);
        Assert.False(sut.IsVideo);
        Assert.Same(sections, sut.SectionCodes);
        Assert.Equal(cancellation.Token, sut.CancellationToken);

        var equivalent = sut with { };
        Assert.Equal(sut, equivalent);

        sut.Deconstruct(out var dbOut, out var npc, out var nlc, out var langOut, out var catName,
            out var isVideo, out var codes, out var ct);
        Assert.Same(db, dbOut);
        Assert.Equal("BI12", npc);
        Assert.Equal("ENG", nlc);
        Assert.Null(langOut);
        Assert.Equal("Bible", catName);
        Assert.False(isVideo);
        Assert.Same(sections, codes);
        Assert.Equal(cancellation.Token, ct);
    }

    [Fact]
    public async Task FetchEnglishPublicationTracksRequest_Holds_Structural_State()
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
            LanguageCode = "TRA-REQ-ST",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var category = new Category { CategoryCode = "CatTrackReq1" };

        var sut = new FetchEnglishPublicationTracksRequest(
            Db: db,
            NormalizedPublicationCode: "IAM",
            NormalizedLanguageCode: "ENG",
            Language: language,
            Category: category,
            CategoryName: "Music",
            IsVideo: true,
            CancellationToken: CancellationToken.None);

        Assert.Same(db, sut.Db);
        Assert.Equal("IAM", sut.NormalizedPublicationCode);
        Assert.Equal("ENG", sut.NormalizedLanguageCode);
        Assert.Same(language, sut.Language);
        Assert.Same(category, sut.Category);
        Assert.Equal("Music", sut.CategoryName);
        Assert.True(sut.IsVideo);
        Assert.Equal(CancellationToken.None, sut.CancellationToken);

        var equivalent = sut with { };
        Assert.Equal(sut, equivalent);

        sut.Deconstruct(out var dbOut, out var npc, out var nlc, out var langOut, out var catOut,
            out var catName, out var isVideo, out var ct);
        Assert.Same(db, dbOut);
        Assert.Equal("IAM", npc);
        Assert.Equal("ENG", nlc);
        Assert.Same(language, langOut);
        Assert.Same(category, catOut);
        Assert.Equal("Music", catName);
        Assert.True(isVideo);
        Assert.Equal(CancellationToken.None, ct);
    }
}
