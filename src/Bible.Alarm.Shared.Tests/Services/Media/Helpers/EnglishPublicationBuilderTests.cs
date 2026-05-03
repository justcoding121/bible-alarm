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

public sealed class EnglishPublicationBuilderTests
{
    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new EnglishPublicationBuilder(null!));
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_ReturnsFalse_When_Sections_Empty()
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
            LanguageCode = "ENG-BLDR-0",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var req = new BuildEnglishPublicationRequest(
            db,
            AppConstants.Media.BiblePublicationCodeNwt,
            PublicationName: "NWT",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: [],
            CancellationToken: CancellationToken.None);

        Assert.False(await sut.BuildAndSavePublicationAsync(req));
        Assert.Equal(0, await db.BiblePublications.CountAsync());
    }

    [Fact]
    public async Task BuildAndSavePublicationAsync_Inserts_Then_Updates_Sections_For_Same_Publication()
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

        var pubCode = AppConstants.Media.BiblePublicationCodeNwt;
        foreach (var categoryCode in JwSourceHelper.GetCategoryCodesForPublication(pubCode))
        {
            if (await db.Categories.AnyAsync(c => c.CategoryCode == categoryCode))
            {
                continue;
            }

            db.Categories.Add(new Category { CategoryCode = categoryCode });
        }

        var lang = new Language
        {
            LanguageCode = "ENG-BLDR-1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(lang);
        await db.SaveChangesAsync();

        var sut = new EnglishPublicationBuilder(TestLogging.CreateLogger());
        var sectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber;

        var firstSections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Genesis first pass",
                SectionCode = sectionCode,
                Tracks = [],
            },
        };

        var reqInsert = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "First label",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: firstSections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(reqInsert));

        var secondSections = new List<BiblePublicationSection>
        {
            new()
            {
                Name = "Genesis second pass",
                SectionCode = sectionCode,
                Tracks = [],
            },
        };

        var reqUpdate = new BuildEnglishPublicationRequest(
            db,
            pubCode,
            PublicationName: "Second label",
            Language: lang,
            IsVideo: false,
            IsBible: true,
            PublicationWithoutLanguage: false,
            Sections: secondSections,
            CancellationToken: CancellationToken.None);

        Assert.True(await sut.BuildAndSavePublicationAsync(reqUpdate));

        var persisted = await db.BiblePublications
            .Include(p => p.Sections)
            .SingleAsync(p => p.PublicationCode == pubCode && p.LanguageId == lang.Id);

        Assert.Equal("Second label", persisted.Name);
        var section = Assert.Single(persisted.Sections);
        Assert.Equal("Genesis second pass", section.Name);
    }
}
