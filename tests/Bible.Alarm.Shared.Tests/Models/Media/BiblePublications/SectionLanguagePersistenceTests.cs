#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionLanguagePersistenceTests
{
    private static async Task<DbContextOptions<MediaDbContext>> BuildOptionsAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        return options;
    }

    [Fact]
    public async Task Persist_RoundTrips_With_Publication_Language_And_Language()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        const string publicationCode = "pub-sec-lang-rt";
        const string sectionCode = "mat-77";

        await using (var db = new MediaDbContext(options))
        {
            var category = new Category { CategoryCode = "CtSecRt" };
            var language = new Language
            {
                LanguageCode = "SL-RT-E1",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };

            db.Categories.Add(category);
            db.Languages.Add(language);
            await db.SaveChangesAsync();

            var publicationLanguage = new PublicationLanguage
            {
                PublicationCode = publicationCode,
                Category = category,
                Language = language,
                CatalogType = CatalogType.IssueSectioned,
                IsMusic = false,
            };

            db.PublicationLanguages.Add(publicationLanguage);
            await db.SaveChangesAsync();

            db.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = publicationCode,
                SectionCode = sectionCode,
                Language = language,
                PublicationLanguage = publicationLanguage,
            });
            await db.SaveChangesAsync();
        }

        await using var read = new MediaDbContext(options);
        var row = await read.SectionLanguages
            .AsNoTracking()
            .Include(sl => sl.PublicationLanguage)
            .Include(sl => sl.Language)
            .SingleAsync(sl => sl.SectionCode == sectionCode && sl.PublicationCode == publicationCode);

        Assert.Equal(CatalogType.IssueSectioned, row.PublicationLanguage.CatalogType);
        Assert.Equal("SL-RT-E1", row.Language!.LanguageCode);
        Assert.Equal(publicationCode, row.PublicationLanguage.PublicationCode);
    }

    [Fact]
    public async Task Persist_Enforces_Unique_Index_On_Publication_Section_Language()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        var pubLangId = 0;
        var languageId = 0;
        const string publicationCode = "pub-dupl-sl";
        const string sectionCode = "same-sec-sl";

        await using (var db = new MediaDbContext(options))
        {
            var category = new Category { CategoryCode = "CtSecUq" };
            var language = new Language
            {
                LanguageCode = "SL-DUP-E1",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };

            db.Categories.Add(category);
            db.Languages.Add(language);
            await db.SaveChangesAsync();

            var publicationLanguage = new PublicationLanguage
            {
                PublicationCode = publicationCode,
                Category = category,
                Language = language,
                CatalogType = null,
                IsMusic = false,
            };

            db.PublicationLanguages.Add(publicationLanguage);
            await db.SaveChangesAsync();

            db.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = publicationCode,
                SectionCode = sectionCode,
                LanguageId = language.Id,
                PublicationLanguageId = publicationLanguage.Id,
                PublicationLanguage = publicationLanguage,
                Language = language,
            });
            await db.SaveChangesAsync();

            pubLangId = publicationLanguage.Id;
            languageId = language.Id;
        }

        await using var conflicting = new MediaDbContext(options);
        conflicting.SectionLanguages.Add(new SectionLanguage
        {
            PublicationCode = publicationCode,
            SectionCode = sectionCode,
            LanguageId = languageId,
            PublicationLanguageId = pubLangId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }
}
