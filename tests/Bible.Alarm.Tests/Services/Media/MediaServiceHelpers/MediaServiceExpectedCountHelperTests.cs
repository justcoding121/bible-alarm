#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.Services.Media.MediaServiceHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class MediaServiceExpectedCountHelperTests
{
    private static DbContextOptions<MediaDbContext> SqliteMemoryOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task GetExpectedSectionCountForNoLanguagePublicationAsync_counts_distinct_sections_with_null_LanguageId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = SqliteMemoryOptions(connection);
        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string pubCode = "iam-nolang";

        await using (var seed = new MediaDbContext(options))
        {
            var cat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            seed.Categories.Add(cat);

            var pl = new PublicationLanguage
            {
                PublicationCode = pubCode,
                Category = cat,
                LanguageId = null,
                Language = null,
                IsMusic = true,
            };
            seed.PublicationLanguages.Add(pl);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = pubCode,
                SectionCode = "iams-a",
                LanguageId = null,
                PublicationLanguage = pl,
            });
            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = pubCode,
                SectionCode = "iams-b",
                LanguageId = null,
                PublicationLanguage = pl,
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);

        var count = await MediaServiceExpectedCountHelper.GetExpectedSectionCountForNoLanguagePublicationAsync(factory,
            pubCode, CancellationToken.None);

        Assert.Equal(2, count);
    }
}
