#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationCategoryPersistenceTests
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
    public async Task Persist_Link_Publication_And_Category_RoundTrips_Via_Includes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        const string publicationCode = "bpc-rt-main";
        const string categoryCode = "CtBpcRt";

        await using (var db = new MediaDbContext(options))
        {
            var category = new Category { CategoryCode = categoryCode };
            var publication = new BiblePublication
            {
                Name = "Junction Publication",
                PublicationCode = publicationCode,
                LanguageId = null,
                Language = null,
                IsVideo = false,
                IsMusic = false,
            };

            publication.BiblePublicationCategories.Add(new BiblePublicationCategory
            {
                BiblePublication = publication,
                Category = category,
            });

            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync();
        }

        await using var read = new MediaDbContext(options);
        var row = await read.BiblePublications
            .AsNoTracking()
            .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(j => j.Category)
            .SingleAsync(bp => bp.PublicationCode == publicationCode);

        var junction = Assert.Single(row.BiblePublicationCategories);
        Assert.Equal(categoryCode, junction.Category.CategoryCode);
    }

    [Fact]
    public async Task Persist_Composite_Key_RejectDuplicate_Pairs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        var pubId = 0;
        var catId = 0;

        await using (var db = new MediaDbContext(options))
        {
            var category = new Category { CategoryCode = "CtBpcUq" };
            var publication = new BiblePublication
            {
                Name = "Dup Junction Pub",
                PublicationCode = "bpc-dup-uq",
                LanguageId = null,
                Language = null,
                IsVideo = false,
                IsMusic = false,
            };

            publication.BiblePublicationCategories.Add(new BiblePublicationCategory
            {
                BiblePublication = publication,
                Category = category,
            });

            db.BiblePublications.Add(publication);
            await db.SaveChangesAsync();

            pubId = publication.Id;
            catId = category.Id;
        }

        await using var conflicting = new MediaDbContext(options);
        conflicting.BiblePublicationCategories.Add(new BiblePublicationCategory
        {
            BiblePublicationId = pubId,
            CategoryId = catId,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }
}
