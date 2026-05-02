#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class EnglishPublicationPersistRequestsTests
{
    [Fact]
    public async Task EnglishPublicationUpdateRequest_Init_Carries_Publication_Collections_And_Flags()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var existing = new BiblePublication
        {
            Name = "Existing",
            PublicationCode = "ENG-PERS-UP",
            LanguageId = null,
            Language = null,
            IsVideo = false,
            IsMusic = false,
        };

        List<Category> categories = [];
        List<BiblePublicationSection> sections = [];

        using var cts = new CancellationTokenSource();

        var sut = new EnglishPublicationUpdateRequest
        {
            Db = db,
            ExistingPublication = existing,
            Categories = categories,
            Sections = sections,
            FinalPublicationName = "Updated Title",
            IsVideo = false,
            IsBible = true,
            PublicationWithoutLanguage = false,
            NormalizedPublicationCode = "eng-pers-up-norm",
            CancellationToken = cts.Token,
        };

        Assert.Same(db, sut.Db);
        Assert.Same(existing, sut.ExistingPublication);
        Assert.Same(categories, sut.Categories);
        Assert.Same(sections, sut.Sections);
        Assert.Equal("Updated Title", sut.FinalPublicationName);
        Assert.False(sut.IsVideo);
        Assert.True(sut.IsBible);
        Assert.False(sut.PublicationWithoutLanguage);
        Assert.Equal("eng-pers-up-norm", sut.NormalizedPublicationCode);
        Assert.Equal(cts.Token, sut.CancellationToken);
    }

    [Fact]
    public async Task EnglishPublicationInsertRequest_Init_Supports_Language_Optionals_Or_Resolved_Ids()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        List<Category> categories = [];
        List<BiblePublicationSection> sections = [];

        var withoutLanguage = new EnglishPublicationInsertRequest
        {
            Db = db,
            Categories = categories,
            Sections = sections,
            NormalizedPublicationCode = "ins-no-lang",
            FinalPublicationName = "No lang row",
            Language = null,
            LanguageId = null,
            IsVideo = false,
            IsBible = false,
            PublicationWithoutLanguage = true,
            CancellationToken = CancellationToken.None,
        };

        Assert.Null(withoutLanguage.Language);
        Assert.Null(withoutLanguage.LanguageId);
        Assert.True(withoutLanguage.PublicationWithoutLanguage);

        var language = new Language
        {
            LanguageCode = "EPR-INS-N1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var withLanguage = new EnglishPublicationInsertRequest
        {
            Db = db,
            Categories = categories,
            Sections = sections,
            NormalizedPublicationCode = "ins-with-lang",
            FinalPublicationName = "Lang row",
            Language = language,
            LanguageId = 901,
            IsVideo = true,
            IsBible = false,
            PublicationWithoutLanguage = false,
            CancellationToken = CancellationToken.None,
        };

        Assert.Same(language, withLanguage.Language);
        Assert.Equal(901, withLanguage.LanguageId);
        Assert.False(withLanguage.PublicationWithoutLanguage);
        Assert.True(withLanguage.IsVideo);
    }
}
