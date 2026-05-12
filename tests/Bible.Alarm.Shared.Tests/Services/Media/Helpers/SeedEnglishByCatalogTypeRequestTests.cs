#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class SeedEnglishByCatalogTypeRequestTests
{
    [Fact]
    public async Task SeedEnglishByCatalogTypeRequest_StructuralEquality_And_Deconstruct()
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
            LanguageCode = "SECAT-LG1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var category = new Category { CategoryCode = "CtSeedEnglish1" };

        using var ctSource = new CancellationTokenSource();

        var sut = new SeedEnglishByCatalogTypeRequest(
            Db: db,
            CatalogType: CatalogType.MediatorSectioned,
            PublicationCode: "DRAMA-DW",
            PublicationCodeForDb: "DRAMA-DW-STORE",
            NormalizedPublicationCode: "drama-dw-norm",
            NormalizedLanguageCode: "en-glob",
            Language: language,
            Category: category,
            CategoryName: "DramaCategory",
            IsVideo: false,
            CancellationToken: ctSource.Token);

        Assert.Same(db, sut.Db);
        Assert.Equal(CatalogType.MediatorSectioned, sut.CatalogType);
        Assert.Equal("DRAMA-DW", sut.PublicationCode);
        Assert.Equal("DRAMA-DW-STORE", sut.PublicationCodeForDb);
        Assert.Equal("drama-dw-norm", sut.NormalizedPublicationCode);
        Assert.Equal("en-glob", sut.NormalizedLanguageCode);
        Assert.Same(language, sut.Language);
        Assert.Same(category, sut.Category);
        Assert.Equal("DramaCategory", sut.CategoryName);
        Assert.False(sut.IsVideo);
        Assert.Equal(ctSource.Token, sut.CancellationToken);

        sut.Deconstruct(
            out var dbOut,
            out var catalogKind,
            out var pubCode,
            out var pubDb,
            out var normPub,
            out var normLang,
            out var langOut,
            out var catOut,
            out var catNameOut,
            out var isVid,
            out var ctOut);

        Assert.Same(db, dbOut);
        Assert.Equal(CatalogType.MediatorSectioned, catalogKind);
        Assert.Equal("DRAMA-DW", pubCode);
        Assert.Equal("DRAMA-DW-STORE", pubDb);
        Assert.Equal("drama-dw-norm", normPub);
        Assert.Equal("en-glob", normLang);
        Assert.Same(language, langOut);
        Assert.Same(category, catOut);
        Assert.Equal("DramaCategory", catNameOut);
        Assert.False(isVid);
        Assert.Equal(ctSource.Token, ctOut);
    }
}
