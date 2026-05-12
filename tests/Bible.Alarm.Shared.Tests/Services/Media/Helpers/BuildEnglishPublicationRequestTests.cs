#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BuildEnglishPublicationRequestTests
{
    [Fact]
    public async Task BuildEnglishPublicationRequest_Holds_State_Deconstruct_And_With_Equality()
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
            LanguageCode = "BER-LANG-X",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        List<BiblePublicationSection> sections = [];

        using var ctSource = new CancellationTokenSource();

        var sut = new BuildEnglishPublicationRequest(
            Db: db,
            NormalizedPublicationCode: "nwt-ber",
            PublicationName: "English display name",
            Language: language,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: sections,
            CancellationToken: ctSource.Token);

        Assert.Same(db, sut.Db);
        Assert.Equal("nwt-ber", sut.NormalizedPublicationCode);
        Assert.Equal("English display name", sut.PublicationName);
        Assert.Same(language, sut.Language);
        Assert.False(sut.IsVideo);
        Assert.True(sut.IsBible);
        Assert.False(sut.PublicationWithoutLanguage);
        Assert.Same(sections, sut.Sections);
        Assert.Equal(ctSource.Token, sut.CancellationToken);

        sut.Deconstruct(out var dbOut, out var npc, out var pname, out var langOut, out var isVideo,
            out var isBible, out var noLangPub, out var secs, out var ct);
        Assert.Same(db, dbOut);
        Assert.Equal("nwt-ber", npc);
        Assert.Equal("English display name", pname);
        Assert.Same(language, langOut);
        Assert.False(isVideo);
        Assert.True(isBible);
        Assert.False(noLangPub);
        Assert.Same(sections, secs);
        Assert.Equal(ctSource.Token, ct);
    }

    [Fact]
    public async Task BuildEnglishPublicationRequest_Allows_Null_Display_Name_And_Language()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        List<BiblePublicationSection> sections = [];

        var sut = new BuildEnglishPublicationRequest(
            Db: db,
            NormalizedPublicationCode: "iam-flat",
            PublicationName: null,
            Language: null,
            IsVideo: false,
            IsBible: false,
            PublicationWithoutLanguage: true,
            Sections: sections,
            CancellationToken: CancellationToken.None);

        Assert.Null(sut.PublicationName);
        Assert.Null(sut.Language);
        Assert.True(sut.PublicationWithoutLanguage);

        sut.Deconstruct(out _, out _, out var pname, out var lang, out _, out _, out var noLang, out _, out _);
        Assert.Null(pname);
        Assert.Null(lang);
        Assert.True(noLang);
    }
}
