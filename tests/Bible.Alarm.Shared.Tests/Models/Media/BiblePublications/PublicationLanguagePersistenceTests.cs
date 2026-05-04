#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguagePersistenceTests
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
    public async Task Persist_RoundTrips_With_Category_Language_And_Catalog_Type()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        const string publicationCode = "pub-lang-rt-one";

        await using (var db = new MediaDbContext(options))
        {
            var category = new Category { CategoryCode = "PlRtCategory" };
            var language = new Language
            {
                LanguageCode = "PL-RT-E1",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };

            db.Categories.Add(category);
            db.Languages.Add(language);
            await db.SaveChangesAsync();

            db.PublicationLanguages.Add(new PublicationLanguage
            {
                PublicationCode = publicationCode,
                Category = category,
                Language = language,
                CatalogType = CatalogType.MediatorSectioned,
                IsMusic = false,
            });
            await db.SaveChangesAsync();
        }

        await using var read = new MediaDbContext(options);
        var row = await read.PublicationLanguages
            .AsNoTracking()
            .Include(pl => pl.Category)
            .Include(pl => pl.Language)
            .SingleAsync(pl => pl.PublicationCode == publicationCode);

        Assert.Equal(CatalogType.MediatorSectioned, row.CatalogType);
        Assert.False(row.IsMusic);
        Assert.Equal("PlRtCategory", row.Category.CategoryCode);
        Assert.Equal("PL-RT-E1", row.Language!.LanguageCode);
    }

    [Fact]
    public async Task Persist_Enforces_Unique_Index_On_PublicationCode_Language_And_Category()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        await using var db = new MediaDbContext(options);
        var category = new Category { CategoryCode = "PlUqCat" };
        var language = new Language
        {
            LanguageCode = "PL-UQ-E1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        db.Categories.Add(category);
        db.Languages.Add(language);
        await db.SaveChangesAsync();

        const string dupPub = "pub-lang-dup-uq";

        db.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = dupPub,
            Category = category,
            Language = language,
            CatalogType = CatalogType.Flat,
            IsMusic = true,
        });
        db.PublicationLanguages.Add(new PublicationLanguage
        {
            PublicationCode = dupPub,
            CategoryId = category.Id,
            LanguageId = language.Id,
            CatalogType = CatalogType.Sectioned,
            IsMusic = false,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
