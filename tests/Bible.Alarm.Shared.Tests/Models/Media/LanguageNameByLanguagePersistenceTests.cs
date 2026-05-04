#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguagePersistenceTests
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
    public async Task Persist_RoundTrips_Display_Name_With_Language_Navigation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        await using (var db = new MediaDbContext(options))
        {
            var language = new Language
            {
                LanguageCode = "LN-RTL",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };

            db.Languages.Add(language);
            await db.SaveChangesAsync();

            db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
            {
                Language = language,
                DisplayLanguageCode = "E",
                Name = "For display locale E",
            });
            await db.SaveChangesAsync();
        }

        await using var read = new MediaDbContext(options);
        var row = await read.LanguageNamesByLanguage
            .AsNoTracking()
            .Include(entry => entry.Language)
            .SingleAsync(entry => entry.DisplayLanguageCode == "E");

        Assert.Equal("For display locale E", row.Name);
        Assert.Equal("LN-RTL", row.Language.LanguageCode);
    }

    [Fact]
    public async Task Persist_Enforces_Unique_Index_On_Language_And_Display_Code()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = await BuildOptionsAsync(connection);

        var languageId = 0;

        await using (var db = new MediaDbContext(options))
        {
            var language = new Language
            {
                LanguageCode = "LN-DUP-RT",
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            };

            db.Languages.Add(language);
            await db.SaveChangesAsync();

            db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
            {
                LanguageId = language.Id,
                DisplayLanguageCode = "MX",
                Name = "First name",
                Language = language,
            });
            await db.SaveChangesAsync();

            languageId = language.Id;
        }

        await using var conflicting = new MediaDbContext(options);
        conflicting.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
        {
            LanguageId = languageId,
            DisplayLanguageCode = "MX",
            Name = "Collision",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }
}
