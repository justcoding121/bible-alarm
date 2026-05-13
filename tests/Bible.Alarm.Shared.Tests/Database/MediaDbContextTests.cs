#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediaDbContextTests
{
    [Fact]
    public void SaveChanges_second_language_with_duplicate_language_code_fails_unique_index()
    {
        // Keep the connection open so all DbContext operations share the same in-memory database.
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var db = new MediaDbContext(options);
        db.Database.EnsureCreated();

        db.Languages.Add(new Language { LanguageCode = "Z9", Direction = AppConstants.Media.TextDirectionLeftToRight });
        db.SaveChanges();

        db.Languages.Add(new Language { LanguageCode = "Z9", Direction = AppConstants.Media.TextDirectionLeftToRight });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
