#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageTests
{
    [Fact]
    public void SaveChanges_second_row_with_same_language_and_display_code_fails_unique_index()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var db = new MediaDbContext(options);
        db.Database.EnsureCreated();

        var language = new Language
        {
            LanguageCode = "LN1",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        db.Languages.Add(language);
        db.SaveChanges();

        db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
        {
            LanguageId = language.Id,
            Language = language,
            DisplayLanguageCode = "E",
            Name = "First",
        });
        db.SaveChanges();

        db.LanguageNamesByLanguage.Add(new LanguageNameByLanguage
        {
            LanguageId = language.Id,
            Language = language,
            DisplayLanguageCode = "E",
            Name = "Duplicate",
        });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
